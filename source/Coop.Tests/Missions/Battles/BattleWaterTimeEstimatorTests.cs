using Missions.Battles;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class BattleWaterTimeEstimatorTests
{
    private readonly BattleWaterTimeEstimator estimator = new();

    [Fact]
    public void Advance_WithoutHostTime_WritesTheLocalClock()
    {
        Assert.Equal(1000f, estimator.Advance(1000f, isHost: false));
        Assert.Equal(1000.5f, estimator.Advance(1000.5f, isHost: false));
        Assert.False(estimator.HasHostTime);
    }

    [Fact]
    public void FirstHostSample_IsAdoptedAtOnce()
    {
        estimator.Advance(1000f, isHost: false);

        estimator.AddHostSample(hostTime: 5000f, localTime: 1000f);

        Assert.Equal(5000.25f, estimator.Advance(1000.25f, isHost: false));
        Assert.Equal(4000.0, estimator.AppliedOffset);
    }

    [Fact]
    public void LaterSamples_AreSmoothedIntoTheTarget()
    {
        estimator.AddHostSample(5000f, 1000f);
        estimator.Advance(1000f, isHost: false);

        estimator.AddHostSample(5010f, 1000f);

        Assert.Equal(4000.0 + (BattleWaterTimeEstimator.SmoothingFactor * 10.0), estimator.TargetOffset, 6);
    }

    [Fact]
    public void Advance_SlewsTheOffsetByAtMostAFractionOfTheLocalStep()
    {
        estimator.AddHostSample(5000f, 1000f);
        estimator.Advance(1000f, isHost: false);
        estimator.AddHostSample(5010f, 1000f);

        float written = estimator.Advance(1001f, isHost: false);

        Assert.Equal(4000.0 + BattleWaterTimeEstimator.SlewFraction, estimator.AppliedOffset, 6);
        Assert.Equal(1001f + 4000.25f, written);
    }

    [Fact]
    public void Advance_NeverRunsBackwardsWhileSlewingDown()
    {
        estimator.AddHostSample(5000f, 1000f);
        float previous = estimator.Advance(1000f, isHost: false);
        estimator.AddHostSample(4985f, 1000f);

        for (float local = 1000.16f; local < 1100f; local += 0.16f)
        {
            float written = estimator.Advance(local, isHost: false);
            Assert.True(written >= previous, $"{written} < {previous}");
            previous = written;
        }
        Assert.Equal(estimator.TargetOffset, estimator.AppliedOffset, 3);
    }

    [Fact]
    public void Advance_DoesNotSlewWhileTheLocalClockStands()
    {
        estimator.AddHostSample(5000f, 1000f);
        estimator.Advance(1000f, isHost: false);
        estimator.AddHostSample(5010f, 1000f);

        Assert.Equal(5000f, estimator.Advance(1000f, isHost: false));
    }

    [Fact]
    public void ADisagreementBeyondTheSnapThreshold_IsAdoptedAtOnce()
    {
        estimator.AddHostSample(5000f, 1000f);
        estimator.Advance(1000f, isHost: false);

        estimator.AddHostSample(5100f, 1000f);

        Assert.Equal(5101f, estimator.Advance(1001f, isHost: false));
    }

    [Fact]
    public void APromotedHost_ContinuesFromItsOwnCurrentWaterTime()
    {
        estimator.AddHostSample(5000f, 1000f);
        estimator.Advance(1000f, isHost: false);
        estimator.AddHostSample(5010f, 1000f);
        float before = estimator.Advance(1001f, isHost: false);

        float after = estimator.Advance(1002f, isHost: true);

        Assert.Equal(before + 1f, after);
        Assert.Equal(estimator.AppliedOffset, estimator.TargetOffset);
    }
}
