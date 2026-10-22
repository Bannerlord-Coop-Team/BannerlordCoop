using Missions.Battles;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class BattleWaterTimeEstimatorTests
{
    private readonly BattleWaterTimeEstimator estimator = new();

    [Fact]
    public void Begin_WithoutHostTime_StartsAtTheBattleStartTime()
    {
        Assert.Equal(0f, estimator.Begin(localTime: 3000f, startTime: 0f));
        Assert.Equal(0.5f, estimator.Advance(3000.5f, isHost: false));
        Assert.False(estimator.HasHostTime);
    }

    [Fact]
    public void Begin_AfterHostSamplesDuringLoading_StartsAtTheLatestOne()
    {
        // The mission timer stands during loading, so later samples carry larger offsets; the smoothed target would lag.
        estimator.AddHostSample(hostTime: 500f, localTime: 3000f);
        estimator.AddHostSample(hostTime: 510f, localTime: 3000f);

        Assert.Equal(510f, estimator.Begin(3000f, 0f));
        Assert.Equal(510.25f, estimator.Advance(3000.25f, isHost: false));
    }

    [Fact]
    public void FirstAdoption_AheadBeyondTheSnapThreshold_JumpsForwardAtOnce()
    {
        estimator.Begin(1000f, 0f);
        estimator.Advance(1000f, isHost: false);

        estimator.AddHostSample(hostTime: 500f, localTime: 1000f);

        Assert.Equal(500.25f, estimator.Advance(1000.25f, isHost: false));
    }

    [Fact]
    public void FirstAdoption_Behind_IsSlewedAndNeverDecreasesTheWrittenTime()
    {
        estimator.Begin(1000f, 100f);
        float previous = estimator.Advance(1000f, isHost: false);

        estimator.AddHostSample(hostTime: 50f, localTime: 1000f);

        for (float local = 1000.25f; local < 1100f; local += 0.25f)
        {
            float written = estimator.Advance(local, isHost: false);
            Assert.True(written - previous >= (1 - BattleWaterTimeEstimator.SlewFraction) * 0.25f - 0.001f, $"{written} after {previous}");
            previous = written;
        }
    }

    [Fact]
    public void FirstAdoption_WithinTheSnapThreshold_IsSlewed()
    {
        estimator.Begin(1000f, 0f);
        estimator.AddHostSample(hostTime: 10f, localTime: 1000f);

        float written = estimator.Advance(1001f, isHost: false);

        Assert.Equal(1f + (float)BattleWaterTimeEstimator.SlewFraction, written);
    }

    [Fact]
    public void AfterAdoption_ABackwardGapBeyondTheSnapThreshold_NeverDecreasesTheWrittenTime()
    {
        estimator.Begin(1000f, 0f);
        estimator.AddHostSample(5000f, 1000f);
        float previous = estimator.Advance(1000f, isHost: false);

        float local = 1000f;
        for (int second = 1; second <= 60; second++)
        {
            // The host now reports 100 units (10 s) behind this client, every second.
            estimator.AddHostSample(5000f + ((second - 1) * 10f) - 100f, local);
            for (int frame = 0; frame < 6; frame++)
            {
                local += 10f / 6f;
                float written = estimator.Advance(local, isHost: false);
                Assert.True(written - previous >= ((1 - BattleWaterTimeEstimator.SlewFraction) * (10f / 6f)) - 0.01f);
                previous = written;
            }
        }
    }

    [Fact]
    public void AfterAdoption_ALateBurstOfStalledSamples_DoesNotSnap()
    {
        estimator.Begin(1000f, 0f);
        estimator.AddHostSample(5000f, 1000f);
        float previous = estimator.Advance(1000f, isHost: false);

        // Ten seconds of host samples held up in transit, then delivered in one frame: each looks up to 100 units behind.
        float local = 1100f;
        for (int i = 0; i < 10; i++)
            estimator.AddHostSample(5000f + (i * 10f), local);
        float written = estimator.Advance(local, isHost: false);

        Assert.True(written >= previous);
        Assert.True(written - previous <= (100f * (1 + BattleWaterTimeEstimator.SlewFraction)) + 0.01f);
    }

    [Fact]
    public void AfterAdoption_OneForwardOutlier_DoesNotSnap()
    {
        estimator.Begin(1000f, 0f);
        estimator.AddHostSample(5000f, 1000f);
        estimator.Advance(1000f, isHost: false);

        estimator.AddHostSample(5100f, 1000f);
        float written = estimator.Advance(1001f, isHost: false);

        Assert.True(written < 5001f + 20f);
    }

    [Fact]
    public void AfterAdoption_APersistentForwardGap_SnapsForward()
    {
        estimator.Begin(1000f, 0f);
        estimator.AddHostSample(5000f, 1000f);
        estimator.Advance(1000f, isHost: false);

        for (int i = 0; i < BattleWaterTimeEstimator.SampleWindow; i++)
            estimator.AddHostSample(5100f, 1000f);

        Assert.Equal(5101f, estimator.Advance(1001f, isHost: false));
    }

    [Fact]
    public void AfterAHostChange_ANewHostFarAhead_IsAdoptedAtOnce()
    {
        estimator.Begin(1000f, 0f);
        estimator.AddHostSample(5000f, 1000f);
        estimator.Advance(1000f, isHost: false);

        estimator.RearmAdoption();
        estimator.AddHostSample(5500f, 1000f);

        Assert.Equal(5501f, estimator.Advance(1001f, isHost: false));
    }

    [Fact]
    public void AdoptIfAhead_JumpsForwardOnlyBeyondTheSnapThreshold()
    {
        estimator.Begin(1000f, 0f);
        estimator.Advance(1000f, isHost: true);

        Assert.False(estimator.AdoptIfAhead(10f, 1000f));
        Assert.False(estimator.AdoptIfAhead(-500f, 1000f));
        Assert.True(estimator.AdoptIfAhead(3000f, 1000f));

        Assert.Equal(3001f, estimator.Advance(1001f, isHost: true));
    }

    [Fact]
    public void APromotedJoinerThatNeverHeardTheHost_JumpsForwardAndNoClientRunsBackwards()
    {
        // A five-minute-old battle: client A adopted the host's 3000; joiner J started at 0 and never heard the host.
        var clientA = new BattleWaterTimeEstimator();
        clientA.Begin(1000f, 0f);
        clientA.AddHostSample(3000f, 1000f);
        float previousA = clientA.Advance(1000f, isHost: false);
        var joiner = new BattleWaterTimeEstimator();
        joiner.Begin(200f, 0f);
        joiner.Advance(200f, isHost: false);

        // The host leaves and J is promoted: A re-arms and hears J's lagging time first, then reports its own to J.
        clientA.RearmAdoption();
        float joinerTime = joiner.Advance(201f, isHost: true);
        clientA.AddHostSample(joinerTime, 1001f);
        float writtenA = clientA.Advance(1001f, isHost: false);
        Assert.True(writtenA >= previousA);
        previousA = writtenA;

        Assert.True(joiner.AdoptIfAhead(writtenA, 201f));
        float caughtUp = joiner.Advance(202f, isHost: true);
        Assert.True(caughtUp >= writtenA);

        // A keeps running forward and converges on J's time without a backward step.
        float localA = 1001f;
        float localJ = 202f;
        for (int second = 0; second < 30; second++)
        {
            clientA.AddHostSample(joiner.Advance(localJ, isHost: true), localA);
            for (int frame = 0; frame < 6; frame++)
            {
                localA += 10f / 6f;
                localJ += 10f / 6f;
                writtenA = clientA.Advance(localA, isHost: false);
                Assert.True(writtenA >= previousA, $"{writtenA} < {previousA}");
                previousA = writtenA;
            }
        }
        Assert.InRange(writtenA - joiner.Advance(localJ, isHost: true), -1f, 1f);
    }

    [Fact]
    public void Advance_DoesNotSlewWhileTheLocalClockStands()
    {
        estimator.Begin(1000f, 0f);
        estimator.AddHostSample(5000f, 1000f);
        estimator.Advance(1000f, isHost: false);
        estimator.AddHostSample(5010f, 1000f);

        Assert.Equal(5000f, estimator.Advance(1000f, isHost: false));
    }

    [Fact]
    public void LaterSamples_AreSmoothedIntoTheTarget()
    {
        estimator.Begin(1000f, 0f);
        estimator.AddHostSample(5000f, 1000f);
        estimator.Advance(1000f, isHost: false);

        estimator.AddHostSample(5010f, 1000f);

        Assert.Equal(4000.0 + (BattleWaterTimeEstimator.SmoothingFactor * 10.0), estimator.TargetOffset, 6);
    }

    [Fact]
    public void APromotedHost_ContinuesFromItsOwnCurrentWaterTime()
    {
        estimator.Begin(1000f, 0f);
        estimator.AddHostSample(5000f, 1000f);
        estimator.Advance(1000f, isHost: false);
        estimator.AddHostSample(5010f, 1000f);
        float before = estimator.Advance(1001f, isHost: false);

        float after = estimator.Advance(1002f, isHost: true);

        Assert.Equal(before + 1f, after);
        Assert.Equal(estimator.AppliedOffset, estimator.TargetOffset);
    }
}
