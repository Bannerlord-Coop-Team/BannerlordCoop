using Common.Messaging;
using Missions;
using Missions.Battles;
using Missions.Messages;
using Moq;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>
/// Unit tests for <see cref="BattleDeploymentCoordinator"/> — the mesh effects it performs around the pure
/// <see cref="global::GameInterface.Services.MapEvents.BattleDeploymentActivator"/> verdicts, and the commit
/// latch of the "hidden everywhere until deployed" rule (#4): own-party spawns are withheld from peers until
/// the local deployment commit, the first commit asks the caller for exactly one reveal, and the host's NPC/AI
/// is never withheld so it shows frozen on every client during deployment (#1).
/// </summary>
public class BattleDeploymentCoordinatorTests
{
    private readonly Mock<IBattleNetwork> network = new();
    private readonly Mock<IMessageBroker> messageBroker = new();
    private readonly Mock<IBattleSession> session = new();
    private readonly BattleDeploymentCoordinator sut;

    public BattleDeploymentCoordinatorTests()
    {
        session.SetupGet(s => s.OwnControllerId).Returns("us");
        session.SetupGet(s => s.InstanceId).Returns("mapEvent1");
        sut = new BattleDeploymentCoordinator(network.Object, messageBroker.Object, session.Object);
    }

    private void AsHost() => session.SetupGet(s => s.IsLocalHost).Returns(true);

    // --- The withhold/commit latch (absorbs the deleted BattleDeploymentRevealGate's coverage) ---

    [Fact]
    public void OwnPartyTroop_BeforeCommit_IsWithheld()
    {
        Assert.True(sut.ShouldWithhold(isOwnPartyTroop: true));
        Assert.False(sut.IsCommitted);
    }

    [Fact]
    public void NpcTroop_BeforeCommit_IsNotWithheld()
    {
        // Requirement #1: the host's NPC/AI replicates immediately so it shows frozen during deployment.
        Assert.False(sut.ShouldWithhold(isOwnPartyTroop: false));
    }

    [Fact]
    public void ShouldWithhold_IsASideEffectFreeQuery()
    {
        sut.ShouldWithhold(isOwnPartyTroop: true);
        sut.ShouldWithhold(isOwnPartyTroop: true);

        Assert.False(sut.IsCommitted);
        Assert.True(sut.ShouldWithhold(isOwnPartyTroop: true));
    }

    [Fact]
    public void FirstCommit_AsksForTheReveal_ExactlyOnce()
    {
        Assert.True(sut.OnLocalDeploymentFinished());
        Assert.True(sut.IsCommitted);

        // A duplicate finish must not re-reveal (no duplicate puppet broadcast).
        Assert.False(sut.OnLocalDeploymentFinished());
    }

    [Fact]
    public void AfterCommit_NothingIsWithheld()
    {
        sut.OnLocalDeploymentFinished();

        // Own-party reinforcements after commit replicate at once like any other owned spawn.
        Assert.False(sut.ShouldWithhold(isOwnPartyTroop: true));
        Assert.False(sut.ShouldWithhold(isOwnPartyTroop: false));
    }

    // --- Mesh effects around the activator's verdicts ---

    [Fact]
    public void LocalFinish_AnnouncesToTheMesh()
    {
        sut.OnLocalDeploymentFinished();

        network.Verify(n => n.SendAll(It.IsAny<NetworkBattleDeploymentFinished>()), Times.Once);
    }

    [Fact]
    public void LocalFinish_AsHost_BroadcastsBattleActivated()
    {
        AsHost();

        sut.OnLocalDeploymentFinished();

        Assert.True(sut.IsActivated);
        network.Verify(n => n.SendAll(It.IsAny<NetworkBattleActivated>()), Times.Once);
    }

    [Fact]
    public void LocalFinish_AsNonHost_DoesNotBroadcastActivated()
    {
        sut.OnLocalDeploymentFinished();

        Assert.False(sut.IsActivated);
        network.Verify(n => n.SendAll(It.IsAny<NetworkBattleActivated>()), Times.Never);
    }

    [Fact]
    public void CatchUpJoiner_BeforeTheBattleIsLive_SendsNothing()
    {
        sut.CatchUpJoiner("joiner");

        network.Verify(n => n.Send("joiner", It.IsAny<NetworkBattleActivated>()), Times.Never);
    }

    [Fact]
    public void CatchUpJoiner_AfterTheBattleIsLive_ResendsActivatedToTheJoiner()
    {
        // NetworkBattleActivated is a one-shot broadcast, so a mid-battle joiner missed it; the join handshake
        // re-tells it (BattleActivationJoinTests covers the cross-client path end-to-end).
        AsHost();
        sut.OnLocalDeploymentFinished();

        sut.CatchUpJoiner("joiner");

        network.Verify(n => n.Send("joiner", It.IsAny<NetworkBattleActivated>()), Times.Once);
    }
}
