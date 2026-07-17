using GameInterface.Services.MapEvents;
using Xunit;

namespace Coop.Tests.GameInterface.Services.MapEvents;

/// <summary>
/// Unit tests for <see cref="BattleDeploymentTimer"/> — the pure BR-025 gate: each player's deployment phase
/// is limited by a game-configured duration, beginning when that player becomes mission-ready. The timer is
/// fed elapsed time by the caller (the mission tick), so these tests drive it with a fake clock. A true
/// return from <see cref="BattleDeploymentTimer.Tick"/> asks the caller to auto-finish the local deployment;
/// the caller then reports the outcome via <see cref="BattleDeploymentTimer.OnAutoFinishResult"/>. The gate
/// keeps firing while that outcome is <see cref="DeploymentAutoFinishResult.Retry"/> (the finish could not run
/// yet — e.g. reserves still spawning) and disarms permanently on a terminal outcome
/// (<see cref="DeploymentAutoFinishResult.Finished"/> / <see cref="DeploymentAutoFinishResult.Unavailable"/>),
/// so at most one successful auto-finish ever happens. It never fires before mission-ready, never after a
/// manual finish, and never while the configured limit is zero or negative (the documented "disabled" semantic).
/// </summary>
public class BattleDeploymentTimerTests
{
    private const float Limit = 10f;

    [Fact]
    [Trait("Requirement", "BR-025")]
    public void BelowTheLimit_DoesNotExpire()
    {
        var sut = new BattleDeploymentTimer(Limit);
        sut.OnMissionReady();

        Assert.False(sut.Tick(Limit - 0.1f));
    }

    [Fact]
    [Trait("Requirement", "BR-025")]
    public void AccumulatedTimeReachingTheLimit_FiresThenDisarmsOnFinished()
    {
        var sut = new BattleDeploymentTimer(Limit);
        sut.OnMissionReady();

        Assert.False(sut.Tick(6f));
        Assert.True(sut.Tick(4f)); // 10s accumulated — at the limit

        // The caller ran the native finish and it committed: the gate disarms and stays quiet forever.
        sut.OnAutoFinishResult(DeploymentAutoFinishResult.Finished);
        Assert.False(sut.Tick(0.1f));
        Assert.False(sut.Tick(1000f));
    }

    [Fact]
    [Trait("Requirement", "BR-025")]
    public void ExpiryWhileFinishCannotRunYet_KeepsFiring_UntilTheFinishCommits()
    {
        // Reviewer scenario (PR #2036, ShoT-UPfps): a short limit expires while the native TeamSetupOver is
        // still false (reserves inside CoopBattleMissionSpawnHandler's 15s hold), so the caller's finish
        // no-ops and reports Retry. The gate MUST keep asking on every later tick until the finish actually
        // commits — otherwise the AFK player is never auto-finished once setup completes.
        //
        // PRE-FIX MECHANISM: the old Tick set disarmed=true/running=false and returned true on the first tick
        // past the limit, BEFORE the side effect ran. The first Assert.True below would pass, but the timer
        // would already be disarmed, so the second Tick(0.1f) would return false — this test's second
        // Assert.True fails. WITH the fix Tick no longer disarms; only a terminal OnAutoFinishResult does.
        var sut = new BattleDeploymentTimer(Limit);
        sut.OnMissionReady();

        Assert.True(sut.Tick(Limit));                              // limit reached — asks the caller to finish
        sut.OnAutoFinishResult(DeploymentAutoFinishResult.Retry);  // TeamSetupOver false → no-op

        Assert.True(sut.Tick(0.1f));                               // still armed — asks again
        sut.OnAutoFinishResult(DeploymentAutoFinishResult.Retry);  // still spawning → no-op

        Assert.True(sut.Tick(0.1f));                               // and again — teams set up this time
        sut.OnAutoFinishResult(DeploymentAutoFinishResult.Finished);

        // Exactly one successful auto-finish: the gate is now quiet forever.
        Assert.False(sut.Tick(0.1f));
        Assert.False(sut.Tick(1000f));
    }

    [Fact]
    [Trait("Requirement", "BR-025")]
    public void UnavailableOutcome_DisarmsPermanently_NoRepeatedFiring()
    {
        // A mission with no deployment phase / no DeploymentHandler can never be auto-finished; the caller
        // reports Unavailable and the gate must disarm rather than fire every tick forever.
        var sut = new BattleDeploymentTimer(Limit);
        sut.OnMissionReady();

        Assert.True(sut.Tick(Limit));
        sut.OnAutoFinishResult(DeploymentAutoFinishResult.Unavailable);

        Assert.False(sut.Tick(0.1f));
        Assert.False(sut.Tick(1000f));
    }

    [Fact]
    [Trait("Requirement", "BR-025")]
    public void SingleTickPastTheLimit_Fires()
    {
        var sut = new BattleDeploymentTimer(Limit);
        sut.OnMissionReady();

        Assert.True(sut.Tick(Limit * 5f));
    }

    [Fact]
    [Trait("Requirement", "BR-025")]
    public void AlreadyFinishedDeployment_NeverFires()
    {
        var sut = new BattleDeploymentTimer(Limit);
        sut.OnMissionReady();
        sut.OnDeploymentFinished(); // manual Start Battle before the limit

        Assert.False(sut.Tick(Limit * 5f));
    }

    [Fact]
    [Trait("Requirement", "BR-025")]
    public void FinishBeforeMissionReady_KeepsTheGateDisarmed()
    {
        // A native auto-finish (e.g. a leaderless rejoiner skipping the Order of Battle) can conclude the
        // deployment before/regardless of our clock; a later mission-ready must not re-arm the limit.
        var sut = new BattleDeploymentTimer(Limit);
        sut.OnDeploymentFinished();
        sut.OnMissionReady();

        Assert.False(sut.Tick(Limit * 5f));
    }

    [Fact]
    [Trait("Requirement", "BR-025")]
    public void BeforeMissionReady_TimeDoesNotCount()
    {
        var sut = new BattleDeploymentTimer(Limit);

        // The clock has not started (still on the loading screen): no expiry AND no accumulation.
        Assert.False(sut.Tick(Limit * 5f));

        sut.OnMissionReady();
        Assert.False(sut.Tick(Limit - 0.1f)); // pre-ready time must not have counted
        Assert.True(sut.Tick(0.2f));
    }

    [Fact]
    [Trait("Requirement", "BR-025")]
    public void DurationComesFromTheConfigValue()
    {
        var original = BattleDeploymentConfig.DeploymentTimeLimitSeconds;
        try
        {
            BattleDeploymentConfig.DeploymentTimeLimitSeconds = 5f;

            // The production (parameterless) timer latches the game-configuration value at mission-ready.
            var sut = new BattleDeploymentTimer();
            sut.OnMissionReady();

            Assert.False(sut.Tick(4.9f));
            Assert.True(sut.Tick(0.2f));
        }
        finally
        {
            BattleDeploymentConfig.DeploymentTimeLimitSeconds = original;
        }
    }

    [Fact]
    [Trait("Requirement", "BR-025")]
    public void ZeroLimit_DisablesTheGate()
    {
        var sut = new BattleDeploymentTimer(0f);
        sut.OnMissionReady();

        Assert.False(sut.Tick(float.MaxValue / 2f));
    }

    [Fact]
    [Trait("Requirement", "BR-025")]
    public void NegativeLimit_DisablesTheGate()
    {
        var sut = new BattleDeploymentTimer(-1f);
        sut.OnMissionReady();

        Assert.False(sut.Tick(float.MaxValue / 2f));
    }
}
