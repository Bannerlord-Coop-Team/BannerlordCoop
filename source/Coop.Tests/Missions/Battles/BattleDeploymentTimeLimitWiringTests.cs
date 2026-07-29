using Common.Messaging;
using GameInterface.Services.MapEvents;
using Missions;
using Missions.Battles;
using Missions.Messages;
using Moq;
using System;
using System.Collections.Generic;
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

    // Models the native TeamSetupOver: true by default (teams already set up — the finish commits immediately,
    // preserving the intent of the pre-existing cases); flip to false to emulate the limit expiring while
    // reserves are still inside the spawn handler's hold.
    private bool teamSetupOver = true;
    private readonly Queue<Action> deferredActions = new();

    private int nativeFinishInvocations; // every seam call, including the no-op retries while teams aren't set up
    private int nativeFinishCommits;     // seam calls that actually committed the deployment (reported Finished)
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
            FakeNativeFinish,
            deferredActions.Enqueue);
    }

    private void AsHost() => session.SetupGet(s => s.IsLocalHost).Returns(true);

    private void TickAndRunDeferredFinish(float dt)
    {
        sut.Tick(dt);
        if (deferredActions.Count > 0)
            deferredActions.Dequeue()();
    }

    // Emulates the native FinishDeployment seam. When the teams are set up it runs the fan-out reaching
    // CoopBattleController.OnDeploymentFinished (calling OnLocalDeploymentFinished() and revealing the withheld
    // own-party troops on a true return) and reports Finished. While the teams are NOT set up yet (reserves
    // still spawning) it no-ops and reports Retry — exactly as the real FinishNativeDeployment does when the
    // native TeamSetupOver is false.
    private DeploymentAutoFinishResult FakeNativeFinish()
    {
        nativeFinishInvocations++;
        if (!teamSetupOver)
            return DeploymentAutoFinishResult.Retry;

        nativeFinishCommits++;
        if (sut.OnLocalDeploymentFinished())
            revealRequests++;
        return DeploymentAutoFinishResult.Finished;
    }

    [Fact]
    [Trait("Requirement", "BR-025")]
    public void Expiry_AsHost_DrivesTheSameCommitPathAsAManualFinish()
    {
        AsHost();
        sut.OnMissionReady();

        TickAndRunDeferredFinish(Limit);

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

        TickAndRunDeferredFinish(Limit);

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
    public void Expiry_DefersAndQueuesAtMostOneFinish_AcrossFurtherTicks()
    {
        sut.OnMissionReady();

        sut.Tick(Limit);
        sut.Tick(Limit);
        sut.Tick(Limit);

        Assert.Equal(0, nativeFinishInvocations);
        Assert.Single(deferredActions);

        deferredActions.Dequeue()();

        Assert.Equal(1, nativeFinishInvocations);
        network.Verify(n => n.SendAll(It.IsAny<NetworkBattleDeploymentFinished>()), Times.Once);
    }

    [Fact]
    [Trait("Requirement", "BR-025")]
    public void ManualFinishWhileAutoFinishIsQueued_CancelsTheDeferredFinish()
    {
        sut.OnMissionReady();
        sut.Tick(Limit);
        Assert.Single(deferredActions);

        Assert.True(sut.OnLocalDeploymentFinished());
        deferredActions.Dequeue()();

        Assert.Equal(0, nativeFinishInvocations);
        network.Verify(n => n.SendAll(It.IsAny<NetworkBattleDeploymentFinished>()), Times.Once);
    }

    [Fact]
    [Trait("Requirement", "BR-025")]
    public void ManualFinishAfterExpiry_StaysHarmless_NoSecondRevealOrActivation()
    {
        AsHost();
        sut.OnMissionReady();
        TickAndRunDeferredFinish(Limit);
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

    [Fact]
    [Trait("Requirement", "BR-025")]
    public void Expiry_WhileTeamsNotSetUp_RetriesUntilSetupCompletes_ThenCommitsExactlyOnce()
    {
        // Reviewer scenario (PR #2036, ShoT-UPfps): a short limit (e.g. 5s) expires while reserves are still
        // inside CoopBattleMissionSpawnHandler's 15s hold, so the native TeamSetupOver is false and the finish
        // no-ops. The gate must keep firing until setup completes and the finish actually commits — otherwise
        // the AFK player is never auto-finished, no announce ever goes out, and BR-024 activation never fires.
        //
        // PRE-FIX MECHANISM: BattleDeploymentTimer.Tick disarmed the gate (running=false) on the FIRST tick
        // past the limit, before the finish's outcome was known. With this harness the first Tick(Limit) fires
        // while teamSetupOver=false → FakeNativeFinish reports Retry (nativeFinishCommits stays 0) → but the
        // gate is already disarmed, so every later Tick returns false; nativeFinishCommits never reaches 1 and
        // the announce is never sent, failing both post-setup assertions below.
        AsHost();
        sut.OnMissionReady();

        teamSetupOver = false;
        TickAndRunDeferredFinish(Limit); // limit reached, but reserves still spawning → Retry (no-op)
        TickAndRunDeferredFinish(1f);    // still spawning → Retry (no-op)
        Assert.Equal(0, nativeFinishCommits);
        network.Verify(n => n.SendAll(It.IsAny<NetworkBattleDeploymentFinished>()), Times.Never);

        teamSetupOver = true; // the spawn hold elapsed; the teams are set up
        TickAndRunDeferredFinish(1f); // now the finish commits through the same path as a manual Start Battle
        Assert.Equal(1, nativeFinishCommits);
        network.Verify(n => n.SendAll(It.IsAny<NetworkBattleDeploymentFinished>()), Times.Once);
        network.Verify(n => n.SendAll(It.IsAny<NetworkBattleActivated>()), Times.Once);
        Assert.True(sut.IsActivated);
        Assert.True(sut.IsCommitted);
        Assert.Equal(1, revealRequests);

        // Exactly one successful auto-finish: the gate is now disarmed, so further ticks do nothing.
        sut.Tick(1f);
        sut.Tick(1f);
        Assert.Equal(1, nativeFinishCommits);
        network.Verify(n => n.SendAll(It.IsAny<NetworkBattleDeploymentFinished>()), Times.Once);
    }
}
