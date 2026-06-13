using Common;
using Common.Logging;
using Serilog;
using System;
using System.Threading;
using System.Timers;
using TaleWorlds.CampaignSystem;
using Timer = System.Timers.Timer;

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
    /// Smoothly corrects the local campaign time toward <paramref name="serverTicks"/>.
    /// Interpolates <c>_numTicks</c> from its current value to the server value over 1 second,
    /// updating every 0.25 seconds. A new call cancels any interpolation already in progress
    /// and restarts from the current local tick value.
    /// </summary>
    /// <param name="serverTicks">The authoritative server tick value to converge toward.</param>
    void SyncCampaignTime(long serverTicks);

    /// <summary>
    /// Advances the campaign time forward by the given number of ticks.
    /// Intended for debugging the time-sync correction from the server/host.
    /// </summary>
    /// <param name="ticks">The number of ticks to add to the current <c>_numTicks</c> value.</param>
    void AdvanceTime(long ticks);
}

/// <inheritdoc cref="IMapTimeTrackerInterface"/>
internal class MapTimeTrackerInterface : IMapTimeTrackerInterface, IDisposable
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapTimeTrackerInterface>();

    private const double InterpolationDurationMs = 1000d;
    private const double InterpolationIntervalMs = 250d;
    private const int TotalSteps = 4;

    private readonly object interpolationLock = new object();

    private Timer interpolationTimer;
    private long startTicks;
    private long targetTicks;
    private long previousTicks;
    private int currentStep;

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
        var tracker = Campaign.Current?.MapTimeTracker;
        if (tracker == null) return;

        lock (interpolationLock)
        {
            // A newer server update replaces any currently-running interpolation,
            // restarting from the current local tick value.
            StopTimer();

            startTicks = tracker._numTicks;
            previousTicks = startTicks;
            targetTicks = serverTicks;
            currentStep = 0;

            interpolationTimer = new Timer(InterpolationIntervalMs) { AutoReset = true };
            interpolationTimer.Elapsed += OnInterpolationStep;
            interpolationTimer.Start();
        }
    }

    private void OnInterpolationStep(object sender, ElapsedEventArgs e)
    {
        lock (interpolationLock)
        {
            // Ignore stale callbacks from a timer that has already been replaced/stopped.
            if (sender != interpolationTimer) return;

            currentStep++;

            double t = currentStep * InterpolationIntervalMs / InterpolationDurationMs;
            if (t > 1d) t = 1d;

            long newTicks = (long)(startTicks + (targetTicks - startTicks) * t);
            long deltaTicks = newTicks - previousTicks;
            previousTicks = newTicks;

            // Field writes happen on the game thread, the only place MapTimeTracker is ticked.
            GameLoopRunner.RunOnMainThread(() =>
            {
                var tracker = Campaign.Current?.MapTimeTracker;
                if (tracker == null) return;

                tracker._deltaTimeInTicks = deltaTicks;
                tracker._numTicks = newTicks;
            });

            if (currentStep >= TotalSteps)
            {
                StopTimer();
            }
        }
    }

    public void AdvanceTime(long ticks)
    {
        // Field write happens on the game thread, the only place MapTimeTracker is ticked.
        GameLoopRunner.RunOnMainThread(() =>
        {
            var tracker = Campaign.Current?.MapTimeTracker;
            if (tracker == null) return;

            tracker._numTicks += ticks;
        });
    }

    private void StopTimer()
    {
        if (interpolationTimer == null) return;

        interpolationTimer.Elapsed -= OnInterpolationStep;
        interpolationTimer.Stop();
        interpolationTimer.Dispose();
        interpolationTimer = null;
    }

    public void Dispose()
    {
        lock (interpolationLock)
        {
            StopTimer();
        }
    }
}
