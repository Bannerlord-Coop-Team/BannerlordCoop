namespace GameInterface.Services.MapEvents;

/// <summary>
/// The per-player deployment time limit gate (BR-025). Implemented by <see cref="BattleDeploymentTimer"/>.
/// </summary>
public interface IBattleDeploymentTimer
{
    /// <summary>This client became mission-ready (finished loading the battle mission): start the clock.</summary>
    void OnMissionReady();

    /// <summary>The local deployment finished (manually or otherwise): disarm — the limit no longer applies.</summary>
    void OnDeploymentFinished();

    /// <summary>
    /// Advance the clock by <paramref name="dt"/> seconds of game-thread mission time. Returns true EXACTLY
    /// ONCE, on the tick where the accumulated time since <see cref="OnMissionReady"/> reaches the limit —
    /// the caller must then finish the local deployment automatically. Always false before mission-ready,
    /// after a finish, after the one firing, or while the limit is disabled.
    /// </summary>
    bool Tick(float dt);
}

/// <summary>
/// Pure decision logic for the deployment time limit (BR-025): each player's deployment phase is limited by
/// a game-configured duration, beginning when that player becomes mission-ready. The clock is PER-PLAYER and
/// LOCAL — each client times its own deployment and auto-finishes itself on expiry; the auto-finish then
/// flows through the exact same commit path as a manual Start Battle (announce → activation BR-024, reveal
/// BR-023), so no new wire messages exist.
/// <para>
/// Inputs in, verdicts out, in the <see cref="BattleDeploymentActivator"/> mold: the caller
/// (<c>BattleDeploymentCoordinator</c>) owns the side effect (invoking the native deployment finish), so this
/// stays free of mission/engine types and is unit-testable with a fake clock. No background timer threads —
/// the caller feeds it elapsed time from the mission behavior tick (a background <c>Timer</c> touching the
/// broker/network hangs the single-threaded test harness).
/// </para>
/// <para>
/// A limit of zero or less (see <see cref="BattleDeploymentConfig.DeploymentTimeLimitSeconds"/>) disables
/// the gate entirely: <see cref="Tick"/> never fires.
/// </para>
/// </summary>
public sealed class BattleDeploymentTimer : IBattleDeploymentTimer
{
    // Explicit test-supplied limit; null = latch BattleDeploymentConfig.DeploymentTimeLimitSeconds when the
    // clock starts (so the production wiring picks up the config value current at mission-ready).
    private readonly float? explicitLimitSeconds;

    private float limitSeconds;
    private float elapsedSeconds;
    private bool running;
    private bool disarmed;

    /// <summary>Production wiring: the limit comes from <see cref="BattleDeploymentConfig.DeploymentTimeLimitSeconds"/>,
    /// read when the clock starts (<see cref="OnMissionReady"/>).</summary>
    public BattleDeploymentTimer()
    {
    }

    /// <summary>Explicit limit, for tests. Zero or negative disables the gate.</summary>
    public BattleDeploymentTimer(float limitSeconds)
    {
        explicitLimitSeconds = limitSeconds;
    }

    public void OnMissionReady()
    {
        if (running || disarmed) return;

        limitSeconds = explicitLimitSeconds ?? BattleDeploymentConfig.DeploymentTimeLimitSeconds;
        elapsedSeconds = 0f;
        running = true;
    }

    public void OnDeploymentFinished()
    {
        disarmed = true;
        running = false;
    }

    public bool Tick(float dt)
    {
        if (!running) return false;         // before mission-ready, after a finish, or after the one firing
        if (limitSeconds <= 0f) return false; // the configured limit disables the gate

        elapsedSeconds += dt;
        if (elapsedSeconds < limitSeconds) return false;

        // Fire exactly once: the caller auto-finishes the deployment; from then on the gate stays quiet.
        disarmed = true;
        running = false;
        return true;
    }
}
