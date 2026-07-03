using GameInterface.Services.MapEvents;
using Xunit;

namespace Coop.Tests.GameInterface.Services.MapEvents;

/// <summary>
/// Unit tests for <see cref="BattleDeploymentActivator"/> — the gate that releases the host-driven NPC AI on the
/// first deployment-finish from ANY client ("NPC parties do not begin moving until any client has finished").
/// Covers host vs non-host, first/duplicate finishes, a client disconnecting without finishing, and host
/// migration (a promoted client releasing — or correctly NOT releasing — the NPCs it adopts). The activator is
/// pure (inputs in, verdicts out): a true return asks the caller for the side effect named by the method's doc.
/// </summary>
public class BattleDeploymentActivatorTests
{
    private readonly BattleDeploymentActivator sut = new();

    [Fact]
    public void HostFinishesOwnDeploymentFirst_MarksLive_AndAsksToBroadcast()
    {
        // The native FinishDeployment that triggered this already freed the host's NPCs — the true return asks
        // only for the battle-activated broadcast, never a surgical release.
        Assert.True(sut.OnLocalDeploymentFinished(isLocalHost: true));
        Assert.True(sut.IsActivated);
    }

    [Fact]
    public void HostsOwnSecondFinish_IsANoOp()
    {
        Assert.True(sut.OnLocalDeploymentFinished(isLocalHost: true));

        Assert.False(sut.OnLocalDeploymentFinished(isLocalHost: true)); // no re-broadcast
        Assert.True(sut.IsActivated);
    }

    [Fact]
    public void HostSeesPeerFinishFirst_AsksToReleaseAndBroadcast_Once()
    {
        // Still deploying ourselves -> the true return asks for the surgical NPC release + the live broadcast.
        Assert.True(sut.OnRemoteDeploymentFinished(isLocalHost: true));
        Assert.True(sut.IsActivated);

        // Further peer finishes are no-ops.
        Assert.False(sut.OnRemoteDeploymentFinished(isLocalHost: true));
    }

    [Fact]
    public void HostsOwnFinish_AfterAPeerAlreadyActivated_DoesNotRebroadcast()
    {
        Assert.True(sut.OnRemoteDeploymentFinished(isLocalHost: true));

        Assert.False(sut.OnLocalDeploymentFinished(isLocalHost: true));
    }

    [Fact]
    public void NonHostFinishesOwnDeployment_NoActivation()
    {
        Assert.False(sut.OnLocalDeploymentFinished(isLocalHost: false));
        Assert.False(sut.IsActivated);
    }

    [Fact]
    public void NonHostSeesPeerFinish_Ignored()
    {
        // A non-host drives no NPCs (theirs are host-driven puppets).
        Assert.False(sut.OnRemoteDeploymentFinished(isLocalHost: false));
        Assert.False(sut.IsActivated);
    }

    [Fact]
    public void NonHostReceivesActivatedBroadcast_RecordsLive_Idempotently()
    {
        sut.OnBattleActivatedReceived();
        sut.OnBattleActivatedReceived();

        Assert.True(sut.IsActivated);

        // Recorded state also suppresses a later would-be activation (e.g. we become host and a peer finishes).
        Assert.False(sut.OnRemoteDeploymentFinished(isLocalHost: true));
    }

    [Fact]
    public void ClientDisconnectsWithoutFinishing_HostsOwnFinishStillActivates()
    {
        // A peer dropped before deploying, so no remote finish ever arrives. The host finishing its own
        // deployment must still start the battle (no deadlock).
        Assert.True(sut.OnLocalDeploymentFinished(isLocalHost: true));
        Assert.True(sut.IsActivated);
    }

    [Fact]
    public void HostMigration_AfterBattleWentLive_NewHostReleasesAdoptedNpcs()
    {
        // The old host released the NPCs and broadcast that the battle is live; we recorded it as a client.
        sut.OnBattleActivatedReceived();

        // The old host disconnects and we are promoted. Our adopted NPCs must be released even though we may
        // still be in our own deployment (the deployment AI gate would otherwise hold them frozen).
        Assert.True(sut.OnPromotedToHost());
    }

    [Fact]
    public void HostMigration_BeforeBattleWentLive_NewHostWaitsForFirstFinish()
    {
        // Nobody finished before the old host left, so the battle is not live. The new host must NOT release the
        // NPCs on promotion (that would break the gate); it releases on the first finish that arrives afterwards.
        Assert.False(sut.OnPromotedToHost());
        Assert.False(sut.IsActivated);

        Assert.True(sut.OnRemoteDeploymentFinished(isLocalHost: true));
        Assert.True(sut.IsActivated);
    }
}
