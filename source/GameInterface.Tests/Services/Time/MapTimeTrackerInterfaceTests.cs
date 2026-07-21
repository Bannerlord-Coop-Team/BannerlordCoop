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
    public void PrepareCorrection_SlowerServerDoesNotShrinkClientLeadBand()
    {
        var tracker = CreateSynchronizedTracker();

        for (int frame = 0; frame < 5; frame++)
        {
            tracker.GetTickCorrection(10L, 0.05f);
        }

        tracker.BeginCorrection(125L, 1000L);
        tracker.PrepareCorrection(170L, 10L);

        Assert.Equal(0L, tracker.GetTickCorrection(10L, 0.05f));
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

        tracker.PrepareCorrection(250L, 10L);

        Assert.Equal(-1L, tracker.GetTickCorrection(10L, 0.05f));
    }

    [Fact]
    public void PrepareCorrection_ClientFarAheadPausesUntilItReentersResumeBand()
    {
        var tracker = CreateTrackerWithServerRate();

        tracker.PrepareCorrection(420L, 10L);
        Assert.Equal(-10L, tracker.GetTickCorrection(10L, 0.05f));

        Assert.Equal(-50L, tracker.GetTickCorrection(50L, 0.25f));
        tracker.BeginCorrection(220L, 1000L);
        tracker.PrepareCorrection(250L, 10L);

        Assert.Equal(0L, tracker.GetTickCorrection(10L, 0.05f));
    }

    [Fact]
    public void PrepareCorrection_OneWayLatencyProjectsServerTimeForward()
    {
        var tracker = CreateTrackerWithServerRate();

        tracker.BeginCorrection(150L, 1000L, 0.075f);
        tracker.PrepareCorrection(230L, 10L);

        Assert.Equal(0L, tracker.GetTickCorrection(10L, 0.05f));
    }

    [Fact]
    public void PrepareCorrection_BehindClientNeverExceedsDoubleSpeed()
    {
        var tracker = CreateTrackerWithServerRate();

        tracker.PrepareCorrection(0L, 10L);

        Assert.Equal(2L, tracker.GetTickCorrection(10L, 0.05f));
    }

    [Fact]
    public void GetTickCorrection_StaleHeartbeatStopsSimulation()
    {
        var tracker = CreateTrackerWithServerRate();

        Assert.Equal(0L, tracker.GetTickCorrection(10L, 0.8f));
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
    public void ResetForCampaignJoin_ClearsPreviousServerSample()
    {
        var tracker = CreateSynchronizedTracker();
        Assert.False(tracker.BeginCorrection(110L, 1000L));
        tracker.ResetForCampaignJoin();
        Assert.True(tracker.BeginCorrection(120L, 1000L));
        Assert.True(tracker.TryConsumeHardSync(out long serverTicks));
        Assert.Equal(120L, serverTicks);
    }

    [Fact]
    public void ResetForCampaignJoin_DropsHeartbeatsUntilJoinBaseline()
    {
        var tracker = CreateSynchronizedTracker();
        tracker.ResetForCampaignJoin();
        tracker.SyncCampaignTime(120L, 0f);
        Assert.False(tracker.TryConsumeHardSync(out _));
    }

    [Fact]
    public void CompleteCampaignJoinBaseline_WaitsForPostBaselineHeartbeatBeforeCompletingJoin()
    {
        var tracker = CreateJoinTracker(120L);
        AssertJoinStatus(tracker, 120L, 10L, false, false);
        tracker.BeginCorrection(130L, 1000L);
        AssertJoinStatus(tracker, 130L, 10L, true, false);
    }

    [Fact]
    public void CompleteCampaignJoinBaseline_ExcessivelyStaleHeartbeatRequestsRefreshImmediately()
    {
        var tracker = CreateJoinTracker(100L);
        for (int frame = 0; frame < 5; frame++)
        {
            tracker.GetTickCorrection(10L, 0.05f);
        }
        tracker.BeginCorrection(1000L, 10000L);
        AssertJoinStatus(tracker, 100L, 10L, true, true);
    }

    [Fact]
    public void CompleteCampaignJoinBaseline_FixedTransitDelayCatchesUpWithoutAnotherRefresh()
    {
        var tracker = CreateJoinTracker(100L);
        tracker.GetTickCorrection(10L, 0.1f);
        tracker.BeginCorrection(200L, 1000L);
        AssertJoinStatus(tracker, 110L, 10L, false, false);
        AssertJoinStatus(tracker, 200L, 10L, true, false);
    }

    [Fact]
    public void CompleteCampaignJoinBaseline_PausedClientRequestsRefreshInsteadOfWaiting()
    {
        var tracker = CreateJoinTracker(100L);
        tracker.GetTickCorrection(10L, 0.1f);
        tracker.BeginCorrection(200L, 1000L);
        AssertJoinStatus(tracker, 110L, 0L, false, false);
        tracker.BeginCorrection(200L, 1000L);
        AssertJoinStatus(tracker, 110L, 0L, true, true);
    }

    [Fact]
    public void CompleteCampaignJoinBaseline_IgnoresHeartbeatOlderThanBaseline()
    {
        var tracker = CreateJoinTracker(200L);
        Assert.False(tracker.BeginCorrection(190L, 1000L));
        AssertJoinStatus(tracker, 200L, 10L, false, false);
        Assert.False(tracker.TryConsumeHardSync(out _));

        Assert.False(tracker.BeginCorrection(210L, 1000L));
        AssertJoinStatus(tracker, 210L, 10L, true, false);
        Assert.False(tracker.TryConsumeHardSync(out _));
    }

    [Fact]
    public void CompleteCampaignJoinBaseline_NormalCorrectionResumesAfterCurrentHeartbeat()
    {
        var tracker = CreateJoinTracker(200L);
        Assert.False(tracker.BeginCorrection(210L, 100L));
        Assert.False(tracker.TryConsumeHardSync(out _));
        AssertJoinStatus(tracker, 210L, 10L, true, false);

        Assert.True(tracker.BeginCorrection(1000L, 100L));
        Assert.True(tracker.TryConsumeHardSync(out long serverTicks));
        Assert.Equal(1000L, serverTicks);
    }

    [Fact]
    public void CompleteCampaignJoinBaseline_ForwardDiscontinuityRequestsRefreshWithoutHardSync()
    {
        var tracker = CreateJoinTracker(100L);
        Assert.False(tracker.BeginCorrection(2000L, 100L));
        Assert.False(tracker.TryConsumeHardSync(out _));
        AssertJoinStatus(tracker, 100L, 10L, true, true);
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

    private static MapTimeTrackerInterface CreateJoinTracker(long baselineTicks)
    {
        var tracker = CreateSynchronizedTracker();
        tracker.ResetForCampaignJoin();
        tracker.CompleteCampaignJoinBaseline(baselineTicks);
        return tracker;
    }

    private static void AssertJoinStatus(
        MapTimeTrackerInterface tracker,
        long localTicks,
        long localDeltaTicks,
        bool expectedComplete,
        bool expectedRefresh)
    {
        Assert.Equal(expectedComplete,
            tracker.TryCompleteCampaignJoinCatchUp(localTicks, localDeltaTicks, out bool refreshRequired));
        Assert.Equal(expectedRefresh, refreshRequired);
    }
}
