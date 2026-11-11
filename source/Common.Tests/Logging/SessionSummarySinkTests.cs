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
