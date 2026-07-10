using GameInterface.Services.Time.Interfaces;
using Xunit;

namespace GameInterface.Tests.Services.Time;

public class MapTimeTrackerInterfaceTests
{
    [Fact]
    public void BeginCorrection_FirstHeartbeatRequestsHardSync()
    {
        var tracker = new MapTimeTrackerInterface();

        Assert.True(tracker.BeginCorrection(100L, 1000L));
        Assert.True(tracker.TryConsumeHardSync(out long serverTicks));
        Assert.Equal(100L, serverTicks);
    }

    [Fact]
    public void BeginCorrection_OrdinaryHeartbeatStartsBoundedCorrection()
    {
        var tracker = CreateSynchronizedTracker();

        Assert.False(tracker.BeginCorrection(150L, 1000L));
        tracker.PrepareCorrection(200L);
        Assert.Equal(-5L, tracker.GetTickCorrection(10L, 0.025f));
    }

    [Fact]
    public void GetTickCorrection_AheadClientNeverRunsBackward()
    {
        var tracker = CreateSynchronizedTracker();
        tracker.BeginCorrection(100L, 1000L);
        tracker.PrepareCorrection(10000L);

        Assert.Equal(-10L, tracker.GetTickCorrection(10L, 0.25f));
    }

    [Fact]
    public void GetTickCorrection_BehindClientNeverExceedsDoubleSpeed()
    {
        var tracker = CreateSynchronizedTracker();
        tracker.BeginCorrection(10000L, 10000L);
        tracker.PrepareCorrection(100L);

        Assert.Equal(10L, tracker.GetTickCorrection(10L, 0.25f));
    }

    [Fact]
    public void GetTickCorrection_AccumulatesFractionalTicks()
    {
        var tracker = CreateSynchronizedTracker();
        tracker.BeginCorrection(101L, 1000L);
        tracker.PrepareCorrection(100L);

        Assert.Equal(0L, tracker.GetTickCorrection(10L, 0.1f));
        Assert.Equal(0L, tracker.GetTickCorrection(10L, 0.1f));
        Assert.Equal(1L, tracker.GetTickCorrection(10L, 0.1f));
    }

    [Fact]
    public void GetTickCorrection_StaleHeartbeatStopsSimulation()
    {
        var tracker = CreateSynchronizedTracker();
        tracker.BeginCorrection(100L, 1000L);
        tracker.PrepareCorrection(100L);

        Assert.Equal(0L, tracker.GetTickCorrection(10L, 0.8f));
        Assert.Equal(-10L, tracker.GetTickCorrection(10L, 0.8f));

        tracker.BeginCorrection(110L, 1000L);
        tracker.PrepareCorrection(110L);
        Assert.Equal(0L, tracker.GetTickCorrection(10L, 0.01f));
    }

    [Fact]
    public void BeginCorrection_LargeServerJumpRequestsHardSync()
    {
        var tracker = CreateSynchronizedTracker();

        Assert.True(tracker.BeginCorrection(1000L, 100L));
    }

    [Fact]
    public void BeginCorrection_QueuedHeartbeatPreservesPendingHardSyncAndUsesLatestTicks()
    {
        var tracker = new MapTimeTrackerInterface();
        tracker.BeginCorrection(100L, 1000L);

        Assert.True(tracker.BeginCorrection(110L, 1000L));
        Assert.True(tracker.TryConsumeHardSync(out long serverTicks));
        Assert.Equal(110L, serverTicks);
    }

    [Fact]
    public void BeginCorrection_QueuedHeartbeatPreservesPendingDiscontinuity()
    {
        var tracker = CreateSynchronizedTracker();
        tracker.BeginCorrection(1000L, 100L);

        Assert.True(tracker.BeginCorrection(1010L, 100L));
        Assert.True(tracker.TryConsumeHardSync(out long serverTicks));
        Assert.Equal(1010L, serverTicks);
    }

    [Fact]
    public void GetTickCorrection_BeforeFirstHeartbeatLeavesSimulationUnchanged()
    {
        var tracker = new MapTimeTrackerInterface();

        Assert.Equal(0L, tracker.GetTickCorrection(10L, 1f));
    }

    [Fact]
    public void GetTickCorrection_RepeatedUnchangedHeartbeatsDoNotAccumulateDrift()
    {
        var tracker = CreateSynchronizedTracker();
        long localTicks = 100L;
        long firstCycleTicks = 0L;

        for (int heartbeat = 0; heartbeat < 8; heartbeat++)
        {
            tracker.BeginCorrection(100L, 1000L);

            for (int frame = 0; frame < 5; frame++)
            {
                const long vanillaDeltaTicks = 10L;
                localTicks += vanillaDeltaTicks;
                tracker.PrepareCorrection(localTicks);
                localTicks += tracker.GetTickCorrection(vanillaDeltaTicks, 0.05f);
            }

            if (heartbeat == 0)
            {
                firstCycleTicks = localTicks;
            }
        }

        Assert.Equal(firstCycleTicks, localTicks);
    }

    [Fact]
    public void GetTickCorrection_PausedClientDoesNotCatchUpOrRunBackward()
    {
        var tracker = CreateSynchronizedTracker();
        tracker.BeginCorrection(200L, 1000L);
        tracker.PrepareCorrection(100L);

        Assert.Equal(0L, tracker.GetTickCorrection(0L, 1f));
        Assert.Equal(0L, tracker.GetTickCorrection(0L, 1f));
    }

    [Fact]
    public void PrepareCorrection_PostHitchVanillaProgressIsNotAppliedTwice()
    {
        var tracker = CreateSynchronizedTracker();
        tracker.BeginCorrection(200L, 1000L);

        tracker.PrepareCorrection(200L);

        Assert.Equal(0L, tracker.GetTickCorrection(100L, 1f));
    }

    [Fact]
    public void PrepareCorrection_PostHitchCorrectsOnlyUnconsumedProgress()
    {
        var tracker = CreateSynchronizedTracker();
        tracker.BeginCorrection(200L, 1000L);

        tracker.PrepareCorrection(150L);

        Assert.Equal(50L, tracker.GetTickCorrection(50L, 1f));
    }

    private static MapTimeTrackerInterface CreateSynchronizedTracker()
    {
        var tracker = new MapTimeTrackerInterface();
        tracker.BeginCorrection(100L, 1000L);
        tracker.TryConsumeHardSync(out _);
        return tracker;
    }
}
