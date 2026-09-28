using Coop.Core.Server.Services.Telemetry;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Coop.Tests.Server.Services.Telemetry;

/// <summary>
/// Tests the uploader DEBUG servers use instead of the production statistics.
/// </summary>
public class DisabledServerTelemetryUploaderTests
{
    private const string OffLine =
        "Server statistics are off in this DEBUG build; no heartbeat or battle count is sent";
    private const string HeartbeatSkipLine =
        "Skipped the first server statistics heartbeat in this DEBUG build; later ones are skipped without a log line";
    private const string BattleSkipLine =
        "Skipped the first battles fought count in this DEBUG build; later ones are skipped without a log line";

    private static readonly ServerTelemetryStatus Status = new ServerTelemetryStatus(
        "41fa0000000000000000000000000000",
        "0.1.4",
        "abc123",
        new DateTime(2026, 8, 29, 22, 15, 0, DateTimeKind.Utc),
        5);

    [Fact]
    public async Task RepeatedCalls_SendNothingAndLogEachSkipOnce()
    {
        var sink = new CaptureSink();
        using var logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(sink).CreateLogger();
        var uploader = new DisabledServerTelemetryUploader(logger);

        Assert.False(uploader.IsConfigured);
        for (int i = 0; i < 2; i++)
        {
            AssertNotSent(await uploader.UploadAsync(Status, CancellationToken.None));
            AssertNotSent(await uploader.RecordBattleStartedAsync(CancellationToken.None));
        }

        Assert.Collection(
            sink.Events,
            logEvent => AssertLine(logEvent, LogEventLevel.Information, OffLine),
            logEvent => AssertLine(logEvent, LogEventLevel.Debug, HeartbeatSkipLine),
            logEvent => AssertLine(logEvent, LogEventLevel.Debug, BattleSkipLine));
    }

    [Fact]
    public void ConcurrentCalls_LogEachSkipOnce()
    {
        var sink = new CaptureSink();
        using var logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(sink).CreateLogger();
        var uploader = new DisabledServerTelemetryUploader(logger);

        Parallel.For(0, 1000, _ =>
        {
            uploader.UploadAsync(Status, CancellationToken.None);
            uploader.RecordBattleStartedAsync(CancellationToken.None);
        });

        Assert.Equal(3, sink.Events.Count);
        Assert.Single(sink.Events, logEvent => logEvent.MessageTemplate.Text == HeartbeatSkipLine);
        Assert.Single(sink.Events, logEvent => logEvent.MessageTemplate.Text == BattleSkipLine);
    }

    private static void AssertNotSent(ServerTelemetryUploadResult result)
    {
        Assert.False(result.Uploaded);
        Assert.False(result.EndpointConfigured);
    }

    // No properties, so no session id, commit, player count, url or key reaches the log.
    private static void AssertLine(LogEvent logEvent, LogEventLevel level, string text)
    {
        Assert.Equal(level, logEvent.Level);
        Assert.Equal(text, logEvent.MessageTemplate.Text);
        Assert.Empty(logEvent.Properties);
    }

    private sealed class CaptureSink : ILogEventSink
    {
        public ConcurrentQueue<LogEvent> Events { get; } = new();
        public void Emit(LogEvent logEvent) => Events.Enqueue(logEvent);
    }
}
