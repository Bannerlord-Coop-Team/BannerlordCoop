using GameInterface.Services.MapEvents;
using Xunit;

namespace Coop.Tests.GameInterface.Services.MapEvents;

/// <summary>
/// Unit tests for <see cref="BattleDeploymentTimer"/> — the pure BR-025 gate: each player's deployment phase
/// is limited by a game-configured duration, beginning when that player becomes mission-ready. The timer is
/// fed elapsed time by the caller (the mission tick), so these tests drive it with a fake clock. A true
/// return from <see cref="BattleDeploymentTimer.Tick"/> asks the caller to auto-finish the local deployment;
/// it fires at most once, never before mission-ready, never after a finish, and never while the configured
/// limit is zero or negative (the documented "disabled" semantic).
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
    public void AccumulatedTimeReachingTheLimit_FiresExactlyOnce()
    {
        var sut = new BattleDeploymentTimer(Limit);
        sut.OnMissionReady();

        Assert.False(sut.Tick(6f));
        Assert.True(sut.Tick(4f)); // 10s accumulated — at the limit

        // Once fired, the gate stays quiet forever.
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
