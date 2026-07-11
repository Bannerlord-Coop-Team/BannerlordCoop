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
    public void PrepareCorrection_ClientWithinLeadBandKeepsVanillaFastForwardRate()
    {
        var tracker = CreateTrackerWithServerRate();

        tracker.PrepareCorrection(190L, 40L);

        Assert.Equal(0L, tracker.GetTickCorrection(40L, 0.05f));
    }

    [Fact]
    public void PrepareCorrection_HealthyFourHertzHeartbeatsDoNotPaceClient()
    {
        var tracker = CreateSynchronizedTracker();
        long localTicks = 100L;

        for (int heartbeat = 0; heartbeat < 8; heartbeat++)
        {
            for (int frame = 0; frame < 5; frame++)
            {
                const long vanillaDeltaTicks = 10L;
                localTicks += vanillaDeltaTicks;
                tracker.PrepareCorrection(localTicks, vanillaDeltaTicks);
                localTicks += tracker.GetTickCorrection(vanillaDeltaTicks, 0.05f);
            }

            Assert.False(tracker.BeginCorrection(localTicks, 1000L));
        }
    }

    [Fact]
    public void PrepareCorrection_ClientAheadOfLeadBandSlowsWithoutPausing()
    {
        var tracker = CreateTrackerWithServerRate();

        tracker.PrepareCorrection(220L, 10L);

        Assert.Equal(-1L, tracker.GetTickCorrection(10L, 0.05f));
    }

    [Fact]
    public void PrepareCorrection_ClientFarAheadPausesUntilItReentersResumeBand()
    {
        var tracker = CreateTrackerWithServerRate();

        tracker.PrepareCorrection(260L, 10L);
        Assert.Equal(-10L, tracker.GetTickCorrection(10L, 0.05f));

        Assert.Equal(-10L, tracker.GetTickCorrection(10L, 0.25f));
        tracker.BeginCorrection(230L, 1000L);
        tracker.PrepareCorrection(250L, 10L);

        Assert.Equal(0L, tracker.GetTickCorrection(10L, 0.05f));
    }

    [Fact]
    public void PrepareCorrection_BehindClientNeverExceedsDoubleSpeed()
    {
        var tracker = CreateTrackerWithServerRate();

        tracker.PrepareCorrection(0L, 10L);

        Assert.Equal(5L, tracker.GetTickCorrection(10L, 0.05f));
    }

    [Fact]
    public void GetTickCorrection_StaleHeartbeatStopsSimulation()
    {
        var tracker = CreateTrackerWithServerRate();

        Assert.Equal(0L, tracker.GetTickCorrection(10L, 0.8f));
        Assert.Equal(-10L, tracker.GetTickCorrection(10L, 0.8f));
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
    public void GetTickCorrection_PausedClientDoesNotCatchUpOrRunBackward()
    {
        var tracker = CreateTrackerWithServerRate();

        tracker.PrepareCorrection(100L, 0L);

        Assert.Equal(0L, tracker.GetTickCorrection(0L, 1f));
        Assert.Equal(0L, tracker.GetTickCorrection(0L, 1f));
    }

    [Fact]
    public void PrepareCorrection_PostHitchVanillaProgressIsNotAppliedTwice()
    {
        var tracker = CreateTrackerWithServerRate();

        tracker.PrepareCorrection(150L, 100L);

        Assert.Equal(0L, tracker.GetTickCorrection(100L, 1f));
    }

    private static MapTimeTrackerInterface CreateSynchronizedTracker()
    {
        var tracker = new MapTimeTrackerInterface();
        tracker.BeginCorrection(100L, 1000L);
        tracker.TryConsumeHardSync(out _);
        return tracker;
    }

    private static MapTimeTrackerInterface CreateTrackerWithServerRate()
    {
        var tracker = CreateSynchronizedTracker();

        for (int frame = 0; frame < 5; frame++)
        {
            tracker.GetTickCorrection(10L, 0.05f);
        }

        tracker.BeginCorrection(150L, 1000L);
        return tracker;
    }
}
