namespace GameInterface.Services.MapEvents;

/// <summary>
/// The outcome the caller reports back to <see cref="IBattleDeploymentTimer.OnAutoFinishResult"/> after it
/// tries to run the native auto-finish requested by a true <see cref="IBattleDeploymentTimer.Tick"/>. It
/// decides whether the gate disarms or keeps firing (BR-025).
/// </summary>
public enum DeploymentAutoFinishResult
{
    /// <summary>The native finish ran and the deployment committed — the gate disarms permanently.</summary>
    Finished,

    /// <summary>
    /// The finish could not run yet but may on a later tick — e.g. the limit elapsed while the teams are
    /// still being set up (reserves inside the spawn handler's hold, native <c>TeamSetupOver</c> false).
    /// The gate stays armed and keeps asking, so the auto-finish is not lost once setup completes.
    /// </summary>
    Retry,

    /// <summary>
    /// The finish can never run in this mission — there is no deployment phase / no deployment handler to
    /// finish. Retrying would spin forever, so the gate disarms permanently.
    /// </summary>
    Unavailable,
}

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
    /// Advance the clock by <paramref name="dt"/> seconds of game-thread mission time. Returns true on EVERY
    /// tick at or after the accumulated time since <see cref="OnMissionReady"/> reaches the limit, asking the
    /// caller to finish the local deployment automatically — and KEEPS asking until the caller reports a
    /// terminal outcome (<see cref="DeploymentAutoFinishResult.Finished"/> or
    /// <see cref="DeploymentAutoFinishResult.Unavailable"/>) via <see cref="OnAutoFinishResult"/>. This is
    /// deliberate: a single no-op finish (the limit elapsing while reserves are still spawning, so native
    /// <c>TeamSetupOver</c> is false) must NOT permanently lose the auto-finish. Always false before
    /// mission-ready, after a manual finish, after a terminal auto-finish outcome, or while the limit is
    /// disabled.
    /// </summary>
    bool Tick(float dt);

    /// <summary>
    /// The caller reports the outcome of the auto-finish it attempted after a true <see cref="Tick"/>.
    /// <see cref="DeploymentAutoFinishResult.Retry"/> leaves the gate armed (it fires again next tick);
    /// <see cref="DeploymentAutoFinishResult.Finished"/> or <see cref="DeploymentAutoFinishResult.Unavailable"/>
    /// disarms it permanently, so at most one successful auto-finish ever happens.
    /// </summary>
    void OnAutoFinishResult(DeploymentAutoFinishResult result);
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
        if (!running) return false;         // before mission-ready, after a finish, or after a terminal outcome
        if (limitSeconds <= 0f) return false; // the configured limit disables the gate

        elapsedSeconds += dt;
        if (elapsedSeconds < limitSeconds) return false;

        // At or past the limit: ask the caller to auto-finish. Do NOT disarm here — keep asking on every
        // subsequent tick until the caller reports a terminal outcome via OnAutoFinishResult. A single no-op
        // finish (e.g. the limit elapsing while reserves are still spawning, so native TeamSetupOver is false)
        // would otherwise leave the AFK player never auto-finished once setup completes. Clamping elapsed keeps
        // the accumulator from drifting unbounded while we retry.
        elapsedSeconds = limitSeconds;
        return true;
    }

    public void OnAutoFinishResult(DeploymentAutoFinishResult result)
    {
        // Retry: the finish could not run yet (e.g. TeamSetupOver false) but may on a later tick — stay armed.
        if (result == DeploymentAutoFinishResult.Retry) return;

        // Finished (the deployment committed — usually already disarmed by OnDeploymentFinished during the
        // finish's synchronous fan-out) or Unavailable (this mission has no deployment to finish): the gate is
        // done. Disarm permanently so at most one successful auto-finish ever happens.
        disarmed = true;
        running = false;
    }
}
