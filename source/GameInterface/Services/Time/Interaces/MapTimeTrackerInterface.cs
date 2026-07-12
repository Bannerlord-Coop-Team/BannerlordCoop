using Common;
using Common.Logging;
using Serilog;
using System;
using System.Threading;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Time.Interfaces;

/// <summary>
/// Reads and corrects the campaign time held by <see cref="MapTimeTracker"/>.
/// </summary>
public interface IMapTimeTrackerInterface : IGameAbstraction
{
    /// <summary>
    /// Gets the current authoritative tick value from the campaign's <see cref="MapTimeTracker"/>.
    /// </summary>
    /// <param name="ticks">The current <c>_numTicks</c> value.</param>
    /// <returns>True if a campaign is loaded and the value could be read, otherwise false.</returns>
    bool TryGetCurrentTicks(out long ticks);

    /// <summary>
    /// Corrects the client campaign simulation toward <paramref name="serverTicks"/>.
    /// </summary>
    /// <param name="serverTicks">The authoritative server tick value to converge toward.</param>
    void SyncCampaignTime(long serverTicks);

    /// <summary>
    /// Applies the pending server-time correction to the current client simulation frame.
    /// </summary>
    /// <param name="campaign">The campaign whose frame delta and map time are being corrected.</param>
    /// <param name="realDt">The real-time frame delta passed to the campaign tick.</param>
    void ApplyClientSimulationTime(Campaign campaign, float realDt);

    /// <summary>
    /// Advances the campaign time forward by the given number of ticks.
    /// Intended for debugging the time-sync correction from the server/host.
    /// </summary>
    /// <param name="ticks">The number of ticks to add to the current <c>_numTicks</c> value.</param>
    void AdvanceTime(long ticks);
}

/// <inheritdoc cref="IMapTimeTrackerInterface"/>
internal class MapTimeTrackerInterface : IMapTimeTrackerInterface
{
    private enum CampaignTimePacingState
    {
        Normal,
        Slowing,
        PausedForServer,
        PausedForHeartbeat,
        CatchingUp,
    }

    private static ILogger Logger => LogManager.GetLogger<MapTimeTrackerInterface>();

    internal const float CorrectionWindowSeconds = 0.25f;
    internal const float HeartbeatTimeoutSeconds = 0.75f;
    private const float PacingLogDebounceSeconds = CorrectionWindowSeconds * 2f;
    private const float MapSecondsPerCampaignDelta = 4320f;

    private bool hasServerTime;
    private bool hasPendingHardSync;
    private bool hasPendingServerTime;
    private bool skipNextHeartbeatAgeIncrement;
    private long previousServerTicks;
    private long pendingServerTicks;
    // How far the server clock advanced between the last two heartbeats — diagnostic only (shows the
    // server's effective speed in the pacing log; a paused server shows 0). Not used for correction:
    // see PrepareCorrection for why extrapolating the target would overshoot.
    private long lastServerProgressTicks;
    private double remainingCorrectionTicks;
    private double correctionTicksPerSecond;
    private double correctionAccumulator;
    private float secondsSinceHeartbeat;
    private float candidatePacingStateSeconds;
    private CampaignTimePacingState loggedPacingState = CampaignTimePacingState.Normal;
    private CampaignTimePacingState candidatePacingState = CampaignTimePacingState.Normal;

    public bool TryGetCurrentTicks(out long ticks)
    {
        ticks = 0;

        var tracker = Campaign.Current?.MapTimeTracker;
        if (tracker == null) return false;

        // Read from the publish timer's thread; the field is written on the game thread, so a
        // volatile read keeps the long read atomic and un-cached without involving the game loop.
        ticks = Volatile.Read(ref tracker._numTicks);
        return true;
    }

    public void SyncCampaignTime(long serverTicks)
    {
        GameThread.RunSafe(() =>
        {
            var tracker = Campaign.Current?.MapTimeTracker;
            if (tracker == null) return;

            long maxServerProgressTicks = CampaignTime.Hours(6f).NumTicks;
            if (maxServerProgressTicks <= 0L) return;

            BeginCorrection(serverTicks, maxServerProgressTicks);
        }, context: nameof(MapTimeTrackerInterface));
    }

    public void ApplyClientSimulationTime(Campaign campaign, float realDt)
    {
        if (campaign == null) throw new ArgumentNullException(nameof(campaign));

        var tracker = campaign.MapTimeTracker;
        if (TryConsumeHardSync(out long hardSyncTicks))
        {
            LogHardSync(tracker._numTicks, hardSyncTicks);
            tracker._numTicks = hardSyncTicks;
            tracker._deltaTimeInTicks = 0L;
            campaign._dt = 0f;
            return;
        }

        long originalDeltaTicks = tracker._deltaTimeInTicks;
        PrepareCorrection(tracker._numTicks);
        long correctionTicks = GetTickCorrection(originalDeltaTicks, realDt);
        long correctedDeltaTicks = originalDeltaTicks + correctionTicks;
        UpdatePacingDiagnostics(tracker._numTicks + correctionTicks, originalDeltaTicks, correctedDeltaTicks, realDt);
        if (correctionTicks == 0) return;

        tracker._numTicks += correctionTicks;
        tracker._deltaTimeInTicks = correctedDeltaTicks;
        campaign._dt = correctedDeltaTicks / (MapSecondsPerCampaignDelta * CampaignTime.TimeTicksPerSecond);
    }

    public void AdvanceTime(long ticks)
    {
        GameThread.RunSafe(() =>
        {
            var tracker = Campaign.Current?.MapTimeTracker;
            if (tracker == null) return;

            tracker._numTicks += ticks;
        }, context: nameof(MapTimeTrackerInterface));
    }

    internal bool BeginCorrection(long serverTicks, long maxServerProgressTicks)
    {
        secondsSinceHeartbeat = 0f;
        skipNextHeartbeatAgeIncrement = true;

        bool isDiscontinuity = hasServerTime == false ||
            serverTicks < previousServerTicks ||
            serverTicks - previousServerTicks > maxServerProgressTicks;

        // Remember the server's progress over the last heartbeat interval for the pacing log.
        lastServerProgressTicks = isDiscontinuity ? 0L : serverTicks - previousServerTicks;

        hasServerTime = true;
        previousServerTicks = serverTicks;
        pendingServerTicks = serverTicks;
        hasPendingHardSync = hasPendingHardSync || isDiscontinuity;
        hasPendingServerTime = false;
        remainingCorrectionTicks = 0d;
        correctionTicksPerSecond = 0d;
        correctionAccumulator = 0d;

        if (hasPendingHardSync)
        {
            return true;
        }

        hasPendingServerTime = true;
        return false;
    }

    internal bool TryConsumeHardSync(out long serverTicks)
    {
        serverTicks = pendingServerTicks;
        if (hasPendingHardSync == false) return false;

        hasPendingHardSync = false;
        skipNextHeartbeatAgeIncrement = false;
        ResetPacingDiagnostics();
        return true;
    }

    internal void PrepareCorrection(long localTicks)
    {
        if (hasPendingServerTime == false) return;

        hasPendingServerTime = false;

        // Deliberately NOT extrapolated by the server's rate: over the correction window the client's
        // vanilla ticking already advances at (approximately) the server's rate, so the gap measured
        // at heartbeat arrival is the whole correction. Aiming past the heartbeat value would count
        // that vanilla progress twice and overshoot by one interval's worth.
        remainingCorrectionTicks = pendingServerTicks - localTicks;
        correctionTicksPerSecond = remainingCorrectionTicks / CorrectionWindowSeconds;
    }

    internal long GetTickCorrection(long localDeltaTicks, float realDt)
    {
        if (hasServerTime == false || realDt <= 0f) return 0L;

        if (skipNextHeartbeatAgeIncrement)
        {
            skipNextHeartbeatAgeIncrement = false;
        }
        else
        {
            secondsSinceHeartbeat += realDt;
        }
        if (localDeltaTicks <= 0L) return 0L;

        if (secondsSinceHeartbeat > HeartbeatTimeoutSeconds)
        {
            return -localDeltaTicks;
        }

        if (remainingCorrectionTicks == 0d) return 0L;

        double requestedCorrection = correctionTicksPerSecond * realDt;
        requestedCorrection = Math.Max(-localDeltaTicks, Math.Min(localDeltaTicks, requestedCorrection));

        if (remainingCorrectionTicks > 0d)
        {
            if (requestedCorrection <= 0d) return 0L;
            requestedCorrection = Math.Min(remainingCorrectionTicks, requestedCorrection);
        }
        else
        {
            if (requestedCorrection >= 0d) return 0L;
            requestedCorrection = Math.Max(remainingCorrectionTicks, requestedCorrection);
        }

        correctionAccumulator += requestedCorrection;

        long correctionTicks = (long)correctionAccumulator;
        correctionTicks = Math.Max(-localDeltaTicks, Math.Min(localDeltaTicks, correctionTicks));

        if (remainingCorrectionTicks > 0d)
        {
            correctionTicks = Math.Min((long)remainingCorrectionTicks, correctionTicks);
        }
        else
        {
            correctionTicks = Math.Max((long)remainingCorrectionTicks, correctionTicks);
        }

        correctionAccumulator -= correctionTicks;
        remainingCorrectionTicks -= correctionTicks;

        if (remainingCorrectionTicks == 0d)
        {
            correctionAccumulator = 0d;
        }

        return correctionTicks;
    }

    private static CampaignTimePacingState DeterminePacingState(
        long originalDeltaTicks,
        long correctedDeltaTicks,
        bool heartbeatStale)
    {
        if (heartbeatStale) return CampaignTimePacingState.PausedForHeartbeat;
        if (correctedDeltaTicks == 0L && originalDeltaTicks > 0L) return CampaignTimePacingState.PausedForServer;
        if (correctedDeltaTicks < originalDeltaTicks) return CampaignTimePacingState.Slowing;
        if (correctedDeltaTicks > originalDeltaTicks) return CampaignTimePacingState.CatchingUp;
        return CampaignTimePacingState.Normal;
    }

    private void UpdatePacingDiagnostics(
        long clientTicks,
        long originalDeltaTicks,
        long correctedDeltaTicks,
        float realDt)
    {
        if (hasServerTime == false || originalDeltaTicks <= 0L || realDt <= 0f) return;

        bool heartbeatStale = secondsSinceHeartbeat > HeartbeatTimeoutSeconds;
        CampaignTimePacingState pacingState = DeterminePacingState(
            originalDeltaTicks,
            correctedDeltaTicks,
            heartbeatStale);

        if (pacingState == candidatePacingState)
        {
            candidatePacingStateSeconds += realDt;
        }
        else
        {
            candidatePacingState = pacingState;
            candidatePacingStateSeconds = realDt;
        }

        bool enteredHeartbeatPause = pacingState == CampaignTimePacingState.PausedForHeartbeat &&
            loggedPacingState != CampaignTimePacingState.PausedForHeartbeat;
        bool shouldLog = pacingState != loggedPacingState &&
            (enteredHeartbeatPause || candidatePacingStateSeconds >= PacingLogDebounceSeconds);
        if (shouldLog == false) return;

        double speedMultiplier = correctedDeltaTicks / (double)originalDeltaTicks;
        Logger.Information(
            "[CampaignPacing] {PreviousPacingState} -> {PacingState}; " +
            "ServerTicks={ServerTicks}, ClientTicks={ClientTicks}, ServerMinusClientTicks={ServerMinusClientTicks}, " +
            "HeartbeatAgeSeconds={HeartbeatAgeSeconds:0.000}, ServerProgressTicks={ServerProgressTicks}, " +
            "VanillaDeltaTicks={VanillaDeltaTicks}, " +
            "AppliedDeltaTicks={AppliedDeltaTicks}, SpeedMultiplier={SpeedMultiplier:0.00}x",
            loggedPacingState,
            pacingState,
            pendingServerTicks,
            clientTicks,
            pendingServerTicks - clientTicks,
            secondsSinceHeartbeat,
            lastServerProgressTicks,
            originalDeltaTicks,
            correctedDeltaTicks,
            speedMultiplier);

        loggedPacingState = pacingState;
    }

    private static void LogHardSync(long clientTicks, long serverTicks)
    {
        Logger.Information(
            "[CampaignPacing] HardSync; ServerTicks={ServerTicks}, ClientTicksBeforeSync={ClientTicksBeforeSync}, CorrectionTicks={CorrectionTicks}",
            serverTicks,
            clientTicks,
            serverTicks - clientTicks);
    }

    private void ResetPacingDiagnostics()
    {
        candidatePacingStateSeconds = 0f;
        loggedPacingState = CampaignTimePacingState.Normal;
        candidatePacingState = CampaignTimePacingState.Normal;
    }
}
