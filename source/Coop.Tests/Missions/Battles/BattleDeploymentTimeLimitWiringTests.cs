using Common.Messaging;
using GameInterface.Services.MapEvents;
using Missions;
using Missions.Battles;
using Missions.Messages;
using Moq;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>
/// Wiring tests for the BR-025 deployment time limit around <see cref="BattleDeploymentCoordinator"/>:
/// expiry must drive the SAME commit path as a manual Start Battle. In the live game the coordinator's
/// expiry invokes the native <c>DeploymentHandler.FinishDeployment()</c> (the exact method the deployment
/// UI's Start Battle button funnels into), whose behavior fan-out re-enters
/// <c>CoopBattleController.OnDeploymentFinished</c> → <c>OnLocalDeploymentFinished()</c> (+ the reveal on a
/// true return). That native link is engine-level and not headlessly observable (manual checklist row
/// BR-025), so these tests inject a finisher that performs exactly that re-entry and assert the commit
/// effects: the mesh announce goes out (the BR-024 activation input), the activator records the finish, and
/// the first-commit reveal is requested (BR-023).
/// </summary>
public class BattleDeploymentTimeLimitWiringTests
{
    private const float Limit = 10f;

    private readonly Mock<IBattleNetwork> network = new();
    private readonly Mock<IMessageBroker> messageBroker = new();
    private readonly Mock<IBattleSession> session = new();
    private readonly BattleDeploymentCoordinator sut;

    private int nativeFinishInvocations;
    private int revealRequests;

    public BattleDeploymentTimeLimitWiringTests()
    {
        session.SetupGet(s => s.OwnControllerId).Returns("us");
        session.SetupGet(s => s.InstanceId).Returns("mapEvent1");
        sut = new BattleDeploymentCoordinator(
            network.Object,
            messageBroker.Object,
            session.Object,
            new BattleDeploymentTimer(Limit),
            FakeNativeFinish);
    }

    private void AsHost() => session.SetupGet(s => s.IsLocalHost).Returns(true);

    // Emulates the native FinishDeployment fan-out reaching CoopBattleController.OnDeploymentFinished: the
    // controller calls OnLocalDeploymentFinished() and reveals the withheld own-party troops on a true return.
    private void FakeNativeFinish()
    {
        nativeFinishInvocations++;
        if (sut.OnLocalDeploymentFinished())
            revealRequests++;
    }

    [Fact]
    [Trait("Requirement", "BR-025")]
    public void Expiry_AsHost_DrivesTheSameCommitPathAsAManualFinish()
    {
        AsHost();
        sut.OnMissionReady();

        sut.Tick(Limit);

        Assert.Equal(1, nativeFinishInvocations);
        // The mesh announce goes out — the same NetworkBattleDeploymentFinished a manual finish sends, so
        // BR-024 activation counts this auto-finish as the (possibly first) deployment finish.
        network.Verify(n => n.SendAll(It.IsAny<NetworkBattleDeploymentFinished>()), Times.Once);
        Assert.True(sut.IsActivated);
        network.Verify(n => n.SendAll(It.IsAny<NetworkBattleActivated>()), Times.Once);
        // The first commit asks the caller for the own-party reveal (BR-023) exactly once.
        Assert.Equal(1, revealRequests);
        Assert.True(sut.IsCommitted);
    }

    [Fact]
    [Trait("Requirement", "BR-025")]
    public void Expiry_AsNonHost_AnnouncesAndReveals_WithoutSelfActivation()
    {
        // A non-host's auto-finish announces to the mesh (the HOST's activator releases the NPCs on the first
        // finish from ANY client — BR-024) and reveals its own troops (BR-023); it activates nothing itself.
        sut.OnMissionReady();

        sut.Tick(Limit);

        Assert.Equal(1, nativeFinishInvocations);
        network.Verify(n => n.SendAll(It.IsAny<NetworkBattleDeploymentFinished>()), Times.Once);
        Assert.Equal(1, revealRequests);
        Assert.False(sut.IsActivated);
        network.Verify(n => n.SendAll(It.IsAny<NetworkBattleActivated>()), Times.Never);
    }

    [Fact]
    [Trait("Requirement", "BR-025")]
    public void ManualFinishBeforeTheLimit_TimerNoOps_ExactlyOneAnnounce()
    {
        AsHost();
        sut.OnMissionReady();

        Assert.True(sut.OnLocalDeploymentFinished()); // the player clicks Start Battle in time

        sut.Tick(Limit * 5f); // the limit passes afterwards

        Assert.Equal(0, nativeFinishInvocations);
        network.Verify(n => n.SendAll(It.IsAny<NetworkBattleDeploymentFinished>()), Times.Once);
    }

    [Fact]
    [Trait("Requirement", "BR-025")]
    public void Expiry_FiresAtMostOnce_AcrossFurtherTicks()
    {
        sut.OnMissionReady();

        sut.Tick(Limit);
        sut.Tick(Limit);
        sut.Tick(Limit);

        Assert.Equal(1, nativeFinishInvocations);
        network.Verify(n => n.SendAll(It.IsAny<NetworkBattleDeploymentFinished>()), Times.Once);
    }

    [Fact]
    [Trait("Requirement", "BR-025")]
    public void ManualFinishAfterExpiry_StaysHarmless_NoSecondRevealOrActivation()
    {
        AsHost();
        sut.OnMissionReady();
        sut.Tick(Limit);
        Assert.Equal(1, revealRequests);

        // A stray manual finish landing after the auto-finish (e.g. an in-flight button click): the
        // coordinator's commit latch keeps it harmless — no second reveal, no re-activation broadcast.
        Assert.False(sut.OnLocalDeploymentFinished());
        Assert.Equal(1, revealRequests);
        network.Verify(n => n.SendAll(It.IsAny<NetworkBattleActivated>()), Times.Once);
    }

    [Fact]
    [Trait("Requirement", "BR-025")]
    public void TickBeforeMissionReady_DoesNothing()
    {
        // No OnMissionReady — the player is still on the loading screen; the limit has not begun.
        sut.Tick(Limit * 10f);

        Assert.Equal(0, nativeFinishInvocations);
        network.Verify(n => n.SendAll(It.IsAny<NetworkBattleDeploymentFinished>()), Times.Never);
    }
}
