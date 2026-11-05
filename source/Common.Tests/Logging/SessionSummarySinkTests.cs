using Common.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Collections.Concurrent;
using Xunit;

namespace Common.Tests.Logging;

public class SessionSummarySinkTests
{
    [Fact]
    public void WriteSummary_CountsLevelsAndErrorsPerSource()
    {
        var sink = new SessionSummarySink();
        sink.Emit(CreateEvent(LogEventLevel.Debug, "Coop.A"));
        sink.Emit(CreateEvent(LogEventLevel.Information, "Coop.A"));
        sink.Emit(CreateEvent(LogEventLevel.Warning, "Coop.A"));
        sink.Emit(CreateEvent(LogEventLevel.Error, "Coop.A"));
        sink.Emit(CreateEvent(LogEventLevel.Error, "Coop.A"));
        sink.Emit(CreateEvent(LogEventLevel.Error, "Coop.B"));
        sink.Emit(CreateEvent(LogEventLevel.Fatal, "Coop.B"));
        sink.Emit(CreateEvent(LogEventLevel.Warning, "Coop.C"));

        var capture = new CaptureSink();
        // The summary goes through a pipeline that also holds the counting sink, so a summary
        // that counted itself would show up in its own totals.
        using var logger = new LoggerConfiguration().WriteTo.Sink(sink).WriteTo.Sink(capture).CreateLogger();

        Assert.True(sink.WriteSummary(logger.ForContext("SourceContext", "Coop.CoopMod"), "exit"));

        var lines = capture.Events.Select(e => e.RenderMessage()).ToArray();
        Assert.Equal(new[]
        {
            "[Session] summary reason=exit warnings=2 errors=3 fatal=1",
            "[Session] errors source=Coop.A n=2",
            "[Session] errors source=Coop.B n=1",
        }, lines);
    }

    [Fact]
    public void WriteSummary_GenericSourceContextHasNoSpaces()
    {
        var sink = new SessionSummarySink();
        using (var source = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger())
        {
            // A closed generic type's FullName carries assembly-qualified type arguments.
            source.ForContext<List<int>>().Error("failed");
        }
        var capture = new CaptureSink();
        using var logger = new LoggerConfiguration().WriteTo.Sink(capture).CreateLogger();

        sink.WriteSummary(logger, "exit");

        var line = capture.Events.Select(e => e.RenderMessage()).Last();
        Assert.StartsWith("[Session] errors source=System.Collections.Generic.List`1[[System.Int32,", line);
        Assert.Matches(@"^\[Session\] errors source=\S+ n=1$", line);
    }

    [Fact]
    public void WriteSummary_WritesOnlyOnce()
    {
        var sink = new SessionSummarySink();
        sink.Emit(CreateEvent(LogEventLevel.Error, "Coop.A"));
        var capture = new CaptureSink();
        using var logger = new LoggerConfiguration().WriteTo.Sink(capture).CreateLogger();

        Assert.True(sink.WriteSummary(logger, "crash"));
        sink.Emit(CreateEvent(LogEventLevel.Error, "Coop.A"));
        Assert.False(sink.WriteSummary(logger, "exit"));

        Assert.Equal(2, capture.Events.Count);
        Assert.Equal("[Session] summary reason=crash warnings=0 errors=1 fatal=0", capture.Events.First().RenderMessage());
    }

    [Fact]
    public void WriteSummary_NoErrorsWritesOnlySummaryLine()
    {
        var sink = new SessionSummarySink();
        sink.Emit(CreateEvent(LogEventLevel.Warning, null));
        var capture = new CaptureSink();
        using var logger = new LoggerConfiguration().WriteTo.Sink(capture).CreateLogger();

        sink.WriteSummary(logger, "exit");

        var summary = Assert.Single(capture.Events);
        Assert.Equal("[Session] summary reason=exit warnings=1 errors=0 fatal=0", summary.RenderMessage());
    }

    private static LogEvent CreateEvent(LogEventLevel level, string? sourceContext)
    {
        var properties = sourceContext == null
            ? Array.Empty<LogEventProperty>()
            : new[] { new LogEventProperty("SourceContext", new ScalarValue(sourceContext)) };
        return new LogEvent(DateTimeOffset.UtcNow, level, null, MessageTemplate.Empty, properties);
    }

    private sealed class CaptureSink : ILogEventSink
    {
        public ConcurrentQueue<LogEvent> Events { get; } = new();
        public void Emit(LogEvent logEvent) => Events.Enqueue(logEvent);
    }
}
