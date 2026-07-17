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
    /// <param name="oneWayLatencySeconds">The estimated time the sample spent travelling from server to client.</param>
    void SyncCampaignTime(long serverTicks, float oneWayLatencySeconds);

    /// <summary>
    /// Applies the pending server-time correction to the current client simulation frame.
    /// </summary>
    /// <param name="campaign">The campaign whose frame delta and map time are being corrected.</param>
    /// <param name="realDt">The real-time frame delta passed to the campaign tick.</param>
    void ApplyClientSimulationTime(Campaign campaign, float realDt);

    /// <summary>
    /// Clears prior campaign samples and holds heartbeats until a join baseline arrives.
    /// </summary>
    void ResetForCampaignJoin();

    /// <summary>
    /// Applies a join baseline directly on the game thread and resumes heartbeat pacing.
    /// </summary>
    void ApplyCampaignJoinBaseline(long serverTicks);

    /// <summary>
    /// Evaluates a post-baseline heartbeat to finish local catch-up or request a fresher baseline.
    /// </summary>
    /// <param name="baselineRefreshRequired">
    /// True when this baseline arrived far enough behind the server that a fresher world baseline is required.
    /// </param>
    /// <returns>True when the caller can finish the join or request the indicated refresh.</returns>
    bool TryCompleteCampaignJoinCatchUp(out bool baselineRefreshRequired);

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

    private sealed class ServerTimeSample
    {
        public long Ticks { get; }
        public float OneWayLatencySeconds { get; }

        public ServerTimeSample(long ticks, float oneWayLatencySeconds)
        {
            Ticks = ticks;
            OneWayLatencySeconds = oneWayLatencySeconds;
        }
    }

    private static ILogger Logger => LogManager.GetLogger<MapTimeTrackerInterface>();

    internal const float MaximumClientLeadSeconds = 0.35f;
    internal const float MaximumJoinBaselineDelaySeconds = 2f;
    internal const float HeartbeatTimeoutSeconds = 1.25f;
    private const float MaximumCompensatedOneWayLatencySeconds = 0.25f;
    private const float ResumeClientLeadRatio = 0.75f;
    private const float PauseClientLeadMultiplier = 3f;
    private const float CorrectionSettleSeconds = 1.5f;
    private const float MaximumSlowdownRatio = 0.25f;
    private const double ServerRateSmoothingRatio = 0.25d;
    private const float PacingLogDebounceSeconds = MaximumClientLeadSeconds * 2f;
    private const float MapSecondsPerCampaignDelta = 4320f;

    private bool hasServerTime;
    private bool hasPendingHardSync;
    private bool hasEstimatedServerRate;
    private bool isPausedForServer;
    private bool skipNextHeartbeatAgeIncrement;
    private long previousServerTicks;
    private long lastServerProgressTicks;
    private long pendingServerTicks;
    private float pendingServerOneWayLatencySeconds;
    private ServerTimeSample latestServerTimeSample;
    private int serverTimeApplyQueued;
    private double localTicksPerSecond;
    private double serverTicksPerSecond;
    private double correctionTicksPerSecond;
    private double correctionAccumulator;
    private float secondsSinceHeartbeat;
    private float secondsSinceServerSample;
    private float candidatePacingStateSeconds;
    private CampaignTimePacingState loggedPacingState = CampaignTimePacingState.Normal;
    private CampaignTimePacingState candidatePacingState = CampaignTimePacingState.Normal;
    private bool waitingForJoinBaseline;
    private bool joinCatchUpActive;
    private bool hasPostJoinBaselineHeartbeat;
    private bool joinBaselineRequiresRefresh;
    private bool skipNextServerRateEstimate;

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

    public void SyncCampaignTime(long serverTicks, float oneWayLatencySeconds)
    {
        if (Volatile.Read(ref waitingForJoinBaseline)) return;

        // The game thread only needs the newest sample; queued historical samples add artificial latency.
        Volatile.Write(ref latestServerTimeSample, new ServerTimeSample(serverTicks, oneWayLatencySeconds));
        QueueLatestServerTime();
    }

    private void QueueLatestServerTime()
    {
        if (Interlocked.CompareExchange(ref serverTimeApplyQueued, 1, 0) != 0) return;

        GameThread.RunSafe(ApplyLatestServerTime, context: nameof(MapTimeTrackerInterface));
    }

    private void ApplyLatestServerTime()
    {
        ServerTimeSample appliedSample = null;
        try
        {
            if (Volatile.Read(ref waitingForJoinBaseline))
            {
                Volatile.Write(ref latestServerTimeSample, null);
                return;
            }

            do
            {
                appliedSample = Volatile.Read(ref latestServerTimeSample);
                var tracker = Campaign.Current?.MapTimeTracker;
                if (tracker == null || appliedSample == null) return;

                long maxServerProgressTicks = CampaignTime.Hours(6f).NumTicks;
                if (maxServerProgressTicks <= 0L) return;

                BeginCorrection(
                    appliedSample.Ticks,
                    maxServerProgressTicks,
                    appliedSample.OneWayLatencySeconds);
            }
            while (ReferenceEquals(appliedSample, Volatile.Read(ref latestServerTimeSample)) == false);
        }
        finally
        {
            Volatile.Write(ref serverTimeApplyQueued, 0);

            if (ReferenceEquals(appliedSample, Volatile.Read(ref latestServerTimeSample)) == false)
            {
                QueueLatestServerTime();
            }
        }
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
        PrepareCorrection(tracker._numTicks, originalDeltaTicks);
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

    public void ResetForCampaignJoin()
    {
        Volatile.Write(ref waitingForJoinBaseline, true);
        Volatile.Write(ref latestServerTimeSample, null);
        hasServerTime = false;
        hasPendingHardSync = false;
        hasEstimatedServerRate = false;
        isPausedForServer = false;
        skipNextHeartbeatAgeIncrement = false;
        joinCatchUpActive = false;
        hasPostJoinBaselineHeartbeat = false;
        joinBaselineRequiresRefresh = false;
        skipNextServerRateEstimate = false;
        previousServerTicks = 0L;
        lastServerProgressTicks = 0L;
        pendingServerTicks = 0L;
        pendingServerOneWayLatencySeconds = 0f;
        localTicksPerSecond = 0d;
        serverTicksPerSecond = 0d;
        correctionTicksPerSecond = 0d;
        correctionAccumulator = 0d;
        secondsSinceHeartbeat = 0f;
        secondsSinceServerSample = 0f;
        ResetPacingDiagnostics();
    }

    public void ApplyCampaignJoinBaseline(long serverTicks)
    {
        var campaign = Campaign.Current;
        var tracker = campaign?.MapTimeTracker;
        if (tracker == null)
        {
            throw new InvalidOperationException("Cannot apply a join baseline without a loaded campaign");
        }

        ResetForCampaignJoin();
        LogHardSync(tracker._numTicks, serverTicks);
        tracker._numTicks = serverTicks;
        tracker._deltaTimeInTicks = 0L;
        campaign._dt = 0f;

        CompleteCampaignJoinBaseline(serverTicks);
    }

    internal void CompleteCampaignJoinBaseline(long serverTicks)
    {
        hasServerTime = true;
        previousServerTicks = serverTicks;
        pendingServerTicks = serverTicks;
        skipNextHeartbeatAgeIncrement = true;
        joinCatchUpActive = true;
        hasPostJoinBaselineHeartbeat = false;
        joinBaselineRequiresRefresh = false;
        skipNextServerRateEstimate = true;
        Volatile.Write(ref waitingForJoinBaseline, false);
    }

    public bool TryCompleteCampaignJoinCatchUp(out bool baselineRefreshRequired)
    {
        baselineRefreshRequired = false;
        var tracker = Campaign.Current?.MapTimeTracker;
        if (tracker == null) return false;

        return TryCompleteCampaignJoinCatchUp(
            tracker._numTicks,
            tracker._deltaTimeInTicks,
            out baselineRefreshRequired);
    }

    internal bool TryCompleteCampaignJoinCatchUp(
        long localTicks,
        long localDeltaTicks,
        out bool baselineRefreshRequired)
    {
        baselineRefreshRequired = false;
        if (!joinCatchUpActive || !hasPostJoinBaselineHeartbeat) return false;
        if (joinBaselineRequiresRefresh)
        {
            joinCatchUpActive = false;
            baselineRefreshRequired = true;
            return true;
        }
        if (hasPendingHardSync) return false;

        double projectedServerTicksPerSecond = hasEstimatedServerRate
            ? serverTicksPerSecond
            : localTicksPerSecond;
        double projectedServerTicks = pendingServerTicks + (projectedServerTicksPerSecond *
            (secondsSinceServerSample + pendingServerOneWayLatencySeconds));
        double maximumOffsetTicks = Math.Max(
            Math.Abs(localDeltaTicks),
            Math.Abs(localTicksPerSecond) * MaximumClientLeadSeconds);
        double offsetTicks = Math.Abs(localTicks - projectedServerTicks);
        if (offsetTicks <= maximumOffsetTicks)
        {
            joinCatchUpActive = false;
            return true;
        }

        if (localDeltaTicks <= 0L && lastServerProgressTicks == 0L)
        {
            joinCatchUpActive = false;
            baselineRefreshRequired = true;
            return true;
        }

        double maximumBaselineDelayTicks = Math.Max(
            maximumOffsetTicks,
            Math.Abs(localTicksPerSecond) *
                (MaximumJoinBaselineDelaySeconds + pendingServerOneWayLatencySeconds));
        if (offsetTicks <= maximumBaselineDelayTicks) return false;

        joinCatchUpActive = false;
        baselineRefreshRequired = true;
        return true;
    }

    internal bool BeginCorrection(
        long serverTicks,
        long maxServerProgressTicks,
        float oneWayLatencySeconds = 0f)
    {
        // Sequenced heartbeats can overtake the reliable baseline. Never let an older sample rewind it.
        if (joinCatchUpActive && serverTicks < previousServerTicks) return false;

        float elapsedSincePreviousHeartbeat = secondsSinceServerSample;
        secondsSinceHeartbeat = 0f;
        secondsSinceServerSample = 0f;
        skipNextHeartbeatAgeIncrement = true;

        bool isDiscontinuity = hasServerTime == false ||
            serverTicks < previousServerTicks ||
            serverTicks - previousServerTicks > maxServerProgressTicks;

        lastServerProgressTicks = isDiscontinuity ? 0L : serverTicks - previousServerTicks;
        hasServerTime = true;
        if (joinCatchUpActive)
        {
            hasPostJoinBaselineHeartbeat = true;
            joinBaselineRequiresRefresh = joinBaselineRequiresRefresh || isDiscontinuity;
        }

        if (isDiscontinuity == false &&
            elapsedSincePreviousHeartbeat > 0f &&
            !skipNextServerRateEstimate)
        {
            UpdateEstimatedServerRate(lastServerProgressTicks, elapsedSincePreviousHeartbeat);
        }
        skipNextServerRateEstimate = false;

        previousServerTicks = serverTicks;
        pendingServerTicks = serverTicks;
        pendingServerOneWayLatencySeconds = Math.Max(
            0f,
            Math.Min(oneWayLatencySeconds, MaximumCompensatedOneWayLatencySeconds));
        hasPendingHardSync = hasPendingHardSync || (isDiscontinuity && !joinCatchUpActive);

        if (hasPendingHardSync)
        {
            return true;
        }

        return false;
    }

    private void UpdateEstimatedServerRate(long progressTicks, float elapsedSeconds)
    {
        double observedTicksPerSecond = progressTicks / elapsedSeconds;
        if (hasEstimatedServerRate && observedTicksPerSecond != 0d)
        {
            serverTicksPerSecond +=
                (observedTicksPerSecond - serverTicksPerSecond) * ServerRateSmoothingRatio;
        }
        else
        {
            serverTicksPerSecond = observedTicksPerSecond;
        }

        hasEstimatedServerRate = true;
    }

    internal bool TryConsumeHardSync(out long serverTicks)
    {
        serverTicks = pendingServerTicks;
        if (hasPendingHardSync == false) return false;

        hasPendingHardSync = false;
        skipNextHeartbeatAgeIncrement = false;
        hasEstimatedServerRate = false;
        isPausedForServer = false;
        localTicksPerSecond = 0d;
        serverTicksPerSecond = 0d;
        correctionTicksPerSecond = 0d;
        correctionAccumulator = 0d;
        ResetPacingDiagnostics();
        return true;
    }

    internal void PrepareCorrection(long localTicks, long localDeltaTicks)
    {
        if (hasServerTime == false || localDeltaTicks <= 0L) return;

        double projectedServerTicksPerSecond = hasEstimatedServerRate ? serverTicksPerSecond : localTicksPerSecond;
        double sampleAgeSeconds = secondsSinceServerSample + pendingServerOneWayLatencySeconds;
        double projectedServerTicks = pendingServerTicks + (projectedServerTicksPerSecond * sampleAgeSeconds);
        double maximumClientLeadTicks = Math.Max(localDeltaTicks, localTicksPerSecond * MaximumClientLeadSeconds);
        double resumeClientLeadTicks = maximumClientLeadTicks * ResumeClientLeadRatio;
        double clientLeadTicks = localTicks - projectedServerTicks;

        if (isPausedForServer)
        {
            if (clientLeadTicks > resumeClientLeadTicks)
            {
                correctionTicksPerSecond = 0d;
                return;
            }

            isPausedForServer = false;
        }

        if (clientLeadTicks > maximumClientLeadTicks * PauseClientLeadMultiplier)
        {
            isPausedForServer = true;
            correctionTicksPerSecond = 0d;
            return;
        }

        if (clientLeadTicks > maximumClientLeadTicks)
        {
            correctionTicksPerSecond = -(clientLeadTicks - maximumClientLeadTicks) / CorrectionSettleSeconds;
            return;
        }

        if (clientLeadTicks < -maximumClientLeadTicks)
        {
            correctionTicksPerSecond = (-clientLeadTicks - maximumClientLeadTicks) / CorrectionSettleSeconds;
            return;
        }

        correctionTicksPerSecond = 0d;
    }

    internal long GetTickCorrection(long localDeltaTicks, float realDt)
    {
        if (hasServerTime == false || realDt <= 0f) return 0L;

        secondsSinceServerSample += realDt;
        if (skipNextHeartbeatAgeIncrement)
        {
            skipNextHeartbeatAgeIncrement = false;
        }
        else
        {
            secondsSinceHeartbeat += realDt;
        }
        if (localDeltaTicks > 0L)
        {
            localTicksPerSecond = localDeltaTicks / realDt;
        }
        if (localDeltaTicks <= 0L) return 0L;

        if (secondsSinceHeartbeat > HeartbeatTimeoutSeconds)
        {
            return -localDeltaTicks;
        }

        if (isPausedForServer) return -localDeltaTicks;
        if (correctionTicksPerSecond == 0d) return 0L;

        double requestedCorrection = correctionTicksPerSecond * realDt;
        double maximumSlowdownTicks = localDeltaTicks * MaximumSlowdownRatio;
        requestedCorrection = Math.Max(-maximumSlowdownTicks, Math.Min(localDeltaTicks, requestedCorrection));

        correctionAccumulator += requestedCorrection;

        long correctionTicks = (long)correctionAccumulator;
        correctionTicks = Math.Max(-(long)maximumSlowdownTicks, Math.Min(localDeltaTicks, correctionTicks));

        correctionAccumulator -= correctionTicks;

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

        double projectedServerTicksPerSecond = hasEstimatedServerRate ? serverTicksPerSecond : localTicksPerSecond;
        double projectedServerTicks = pendingServerTicks + (projectedServerTicksPerSecond *
            (secondsSinceServerSample + pendingServerOneWayLatencySeconds));
        double speedMultiplier = correctedDeltaTicks / (double)originalDeltaTicks;
        Logger.Information(
            "[CampaignPacing] {PreviousPacingState} -> {PacingState}; " +
            "ServerTicks={ServerTicks}, ProjectedServerTicks={ProjectedServerTicks}, ClientTicks={ClientTicks}, " +
            "ServerMinusClientTicks={ServerMinusClientTicks}, ProjectedServerMinusClientTicks={ProjectedServerMinusClientTicks}, " +
            "OneWayLatencySeconds={OneWayLatencySeconds:0.000}, HeartbeatAgeSeconds={HeartbeatAgeSeconds:0.000}, " +
            "ServerProgressTicks={ServerProgressTicks}, " +
            "VanillaDeltaTicks={VanillaDeltaTicks}, " +
            "AppliedDeltaTicks={AppliedDeltaTicks}, SpeedMultiplier={SpeedMultiplier:0.00}x",
            loggedPacingState,
            pacingState,
            pendingServerTicks,
            projectedServerTicks,
            clientTicks,
            pendingServerTicks - clientTicks,
            projectedServerTicks - clientTicks,
            pendingServerOneWayLatencySeconds,
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
