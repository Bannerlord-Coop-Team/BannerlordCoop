using Common;
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
    internal const float CorrectionWindowSeconds = 0.25f;
    internal const float HeartbeatTimeoutSeconds = 0.75f;
    private const float MapSecondsPerCampaignDelta = 4320f;

    private bool hasServerTime;
    private bool hasPendingHardSync;
    private bool hasPendingServerTime;
    private bool skipNextHeartbeatAgeIncrement;
    private long previousServerTicks;
    private long pendingServerTicks;
    private double remainingCorrectionTicks;
    private double correctionTicksPerSecond;
    private double correctionAccumulator;
    private float secondsSinceHeartbeat;

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
            tracker._numTicks = hardSyncTicks;
            tracker._deltaTimeInTicks = 0L;
            campaign._dt = 0f;
            return;
        }

        long originalDeltaTicks = tracker._deltaTimeInTicks;
        PrepareCorrection(tracker._numTicks);
        long correctionTicks = GetTickCorrection(originalDeltaTicks, realDt);
        if (correctionTicks == 0) return;

        long correctedDeltaTicks = originalDeltaTicks + correctionTicks;
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
        return true;
    }

    internal void PrepareCorrection(long localTicks)
    {
        if (hasPendingServerTime == false) return;

        hasPendingServerTime = false;
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
}
