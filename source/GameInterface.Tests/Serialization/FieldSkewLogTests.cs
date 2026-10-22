using GameInterface.Serialization;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Collections.Generic;
using Xunit;

namespace GameInterface.Tests.Serialization;

public class FieldSkewLogTests
{
    private sealed class SkewedA { }
    private sealed class SkewedB { }
    private sealed class Skewed<T> { }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = new List<LogEvent>();
        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }

    [Fact]
    public void FirstTime_IsTrueOncePerTypeAndField()
    {
        Assert.True(FieldSkewLog.FirstTime(typeof(SkewedA), "_syncRoot"));
        Assert.False(FieldSkewLog.FirstTime(typeof(SkewedA), "_syncRoot"));

        Assert.True(FieldSkewLog.FirstTime(typeof(SkewedA), "_version"));
        Assert.True(FieldSkewLog.FirstTime(typeof(SkewedB), "_syncRoot"));
    }

    [Fact]
    public void FirstTime_TreatsEveryClosedGenericAsOneType()
    {
        Assert.True(FieldSkewLog.FirstTime(typeof(Skewed<SkewedA>), "_syncRoot"));
        Assert.False(FieldSkewLog.FirstTime(typeof(Skewed<SkewedB>), "_syncRoot"));
        Assert.False(FieldSkewLog.FirstTime(typeof(Skewed<int>), "_syncRoot"));
    }

    [Fact]
    public void WarnOnce_LogsOneWarningForRepeatedSkew()
    {
        var sink = new CollectingSink();
        var logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();

        FieldSkewLog.WarnOnce(logger, typeof(List<SkewedA>), "_skewTestField");
        FieldSkewLog.WarnOnce(logger, typeof(List<SkewedB>), "_skewTestField");
        FieldSkewLog.WarnOnce(logger, typeof(List<SkewedA>), "_skewTestField");

        var warning = Assert.Single(sink.Events);
        Assert.Equal(LogEventLevel.Warning, warning.Level);
        Assert.Contains("_skewTestField", warning.RenderMessage());
    }
}
