using Missions.Agents;
using Xunit;

namespace Coop.Tests.Missions.Agents;

public class NavalDeckErrorMetricTests
{
    // R43: board_copy's one teleport sample (29-64 m) became maxDeckError although the puppet then matched within millimetres.
    [Fact]
    public void Record_SkipsATransitionSampleInLastAndMax()
    {
        var metric = new NavalDeckErrorMetric();

        metric.Record(0.013f, isTransition: false);
        metric.Record(63.8f, isTransition: true);
        metric.Record(0.0007f, isTransition: false);

        Assert.Equal(0.013f, metric.Max);
        Assert.Equal(0.0007f, metric.Last);
        Assert.Equal(1, metric.Skipped);
    }

    // A steady-state error past the 2 m criterion still shows.
    [Fact]
    public void Record_KeepsTheLargestSteadyStateSample()
    {
        var metric = new NavalDeckErrorMetric();

        metric.Record(3.5f, isTransition: false);
        metric.Record(0.2f, isTransition: false);

        Assert.Equal(3.5f, metric.Max);
        Assert.Equal(0.2f, metric.Last);
        Assert.Equal(0, metric.Skipped);
    }

    // A new battle's deck starts from zero.
    [Fact]
    public void Reset_ClearsEverySample()
    {
        var metric = new NavalDeckErrorMetric();
        metric.Record(1f, isTransition: false);
        metric.Record(40f, isTransition: true);

        metric.Reset();

        Assert.Equal(0f, metric.Max);
        Assert.Equal(0f, metric.Last);
        Assert.Equal(0, metric.Skipped);
    }
}
