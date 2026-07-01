using GameInterface.Services.MapEvents;
using Moq;
using Xunit;

namespace Coop.Tests.GameInterface.Services.MapEvents;

/// <summary>
/// Unit tests for <see cref="BattleDeploymentActivator"/> — the gate that releases the host-driven NPC AI on the
/// first deployment-finish from ANY client ("NPC parties do not begin moving until any client has finished").
/// Covers host vs non-host, first/duplicate finishes, a client disconnecting without finishing, and host
/// migration (a promoted client releasing — or correctly NOT releasing — the NPCs it adopts).
/// </summary>
public class BattleDeploymentActivatorTests
{
    private readonly Mock<IBattleDeploymentBridge> bridge = new(MockBehavior.Strict);
    private readonly BattleDeploymentActivator sut;

    public BattleDeploymentActivatorTests()
    {
        sut = new BattleDeploymentActivator(bridge.Object);
    }

    private void AsHost() => bridge.SetupGet(b => b.IsLocalHost).Returns(true);
    private void AsNonHost() => bridge.SetupGet(b => b.IsLocalHost).Returns(false);

    [Fact]
    public void HostFinishesOwnDeploymentFirst_AnnouncesAndMarksLive_WithoutSurgicalRelease()
    {
        AsHost();
        bridge.Setup(b => b.AnnounceLocalDeploymentFinished());
        bridge.Setup(b => b.BroadcastBattleActivated());

        sut.OnLocalDeploymentFinished();

        Assert.True(sut.IsActivated);
        bridge.Verify(b => b.AnnounceLocalDeploymentFinished(), Times.Once); // told peers we finished
        bridge.Verify(b => b.BroadcastBattleActivated(), Times.Once);        // told peers the battle is live
        bridge.Verify(b => b.ReleaseNpcAi(), Times.Never);                   // native FinishDeployment freed our NPCs
    }

    [Fact]
    public void HostSeesPeerFinishFirst_SurgicallyReleasesOnce_AndBroadcastsLive()
    {
        AsHost();
        bridge.Setup(b => b.BroadcastBattleActivated());
        bridge.Setup(b => b.ReleaseNpcAi());

        sut.OnRemoteDeploymentFinished();

        Assert.True(sut.IsActivated);
        bridge.Verify(b => b.ReleaseNpcAi(), Times.Once);            // still deploying -> surgical NPC release
        bridge.Verify(b => b.BroadcastBattleActivated(), Times.Once);

        // Further peer finishes are no-ops.
        sut.OnRemoteDeploymentFinished();
        bridge.Verify(b => b.ReleaseNpcAi(), Times.Once);
        bridge.Verify(b => b.BroadcastBattleActivated(), Times.Once);
    }

    [Fact]
    public void NonHostFinishesOwnDeployment_AnnouncesOnly_NoActivation()
    {
        AsNonHost();
        bridge.Setup(b => b.AnnounceLocalDeploymentFinished());

        sut.OnLocalDeploymentFinished();

        Assert.False(sut.IsActivated);
        bridge.Verify(b => b.AnnounceLocalDeploymentFinished(), Times.Once);
        bridge.Verify(b => b.BroadcastBattleActivated(), Times.Never);
        bridge.Verify(b => b.ReleaseNpcAi(), Times.Never);
    }

    [Fact]
    public void NonHostSeesPeerFinish_Ignored()
    {
        AsNonHost();

        sut.OnRemoteDeploymentFinished();

        Assert.False(sut.IsActivated);
        bridge.Verify(b => b.ReleaseNpcAi(), Times.Never);
        bridge.Verify(b => b.BroadcastBattleActivated(), Times.Never);
    }

    [Fact]
    public void NonHostReceivesActivatedBroadcast_RecordsLive_WithoutReleasing()
    {
        // No bridge calls expected (strict mock): a non-host drives no NPCs; it only records the state.
        sut.OnBattleActivatedReceived();

        Assert.True(sut.IsActivated);
    }

    [Fact]
    public void DuplicateActivatedBroadcast_IsIdempotent()
    {
        sut.OnBattleActivatedReceived();
        sut.OnBattleActivatedReceived();

        Assert.True(sut.IsActivated);
    }

    [Fact]
    public void ClientDisconnectsWithoutFinishing_HostsOwnFinishStillActivates()
    {
        // A peer dropped before deploying, so no remote finish ever arrives. The host finishing its own
        // deployment must still start the battle (no deadlock).
        AsHost();
        bridge.Setup(b => b.AnnounceLocalDeploymentFinished());
        bridge.Setup(b => b.BroadcastBattleActivated());

        sut.OnLocalDeploymentFinished();

        Assert.True(sut.IsActivated);
        bridge.Verify(b => b.BroadcastBattleActivated(), Times.Once);
    }

    [Fact]
    public void HostMigration_AfterBattleWentLive_NewHostReleasesAdoptedNpcs()
    {
        // The old host released the NPCs and broadcast that the battle is live; we recorded it as a client.
        sut.OnBattleActivatedReceived();

        // The old host disconnects and we are promoted. Our adopted NPCs must be released even though we may
        // still be in our own deployment (the deployment AI gate would otherwise hold them frozen).
        bridge.Setup(b => b.ReleaseNpcAi());
        sut.OnPromotedToHost();

        bridge.Verify(b => b.ReleaseNpcAi(), Times.Once);
        bridge.Verify(b => b.BroadcastBattleActivated(), Times.Never); // already broadcast by the previous host
    }

    [Fact]
    public void HostMigration_BeforeBattleWentLive_NewHostWaitsForFirstFinish()
    {
        // Nobody finished before the old host left, so the battle is not live. The new host must NOT release the
        // NPCs on promotion (that would break the gate); it releases on the first finish that arrives afterwards.
        sut.OnPromotedToHost();

        Assert.False(sut.IsActivated);
        bridge.Verify(b => b.ReleaseNpcAi(), Times.Never);

        AsHost();
        bridge.Setup(b => b.BroadcastBattleActivated());
        bridge.Setup(b => b.ReleaseNpcAi());

        sut.OnRemoteDeploymentFinished();

        Assert.True(sut.IsActivated);
        bridge.Verify(b => b.ReleaseNpcAi(), Times.Once);
        bridge.Verify(b => b.BroadcastBattleActivated(), Times.Once);
    }
}
