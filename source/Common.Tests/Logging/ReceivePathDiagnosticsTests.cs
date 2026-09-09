using Common.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Collections.Concurrent;
using System.Net.Sockets;
using Xunit;

namespace Common.Tests.Logging;

public class ReceivePathDiagnosticsTests
{
    [Fact]
    public void Categories_EmitFirstEvidenceThenAggregateUntilIntervalOrTeardown()
    {
        long now = 0;
        var sink = new CaptureSink();
        using var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        var diagnostics = new ReceivePathDiagnostics(() => now, 100);
        diagnostics.Start(logger, "mission instance=MapEvent_Created_8683 peer=97");
        diagnostics.Record(ReceivePathEvent.MappedReceive, 10);
        diagnostics.Record(ReceivePathEvent.UnmappedDrop, 20);
        for (int i = 0; i < 1000; i++) diagnostics.Record(ReceivePathEvent.UnmappedDrop, 20);
        Assert.Equal(3, sink.Events.Count);
        now = 100;
        diagnostics.Record(ReceivePathEvent.MappedReceive, 10);
        Assert.Equal(4, sink.Events.Count);
        diagnostics.End("peer-removed");
        diagnostics.End("duplicate-end");
        diagnostics.Record(ReceivePathEvent.MappedReceive, 10);
        Assert.Equal(5, sink.Events.Count);
        string summary = sink.Events.Last().RenderMessage();
        Assert.Contains("MappedReceive\": 2", summary);
        Assert.Contains("UnmappedDrop\": 1001", summary);
        Assert.Contains("UnmappedDrop\": 20020", summary);
        Assert.Contains("MapEvent_Created_8683", summary);
        Assert.Contains("utc=", summary);
    }

    [Fact]
    public void Restart_ResetsTotalsFirstEvidenceAndCorrelation()
    {
        var sink = new CaptureSink();
        using var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        var diagnostics = new ReceivePathDiagnostics(() => 0, 100);
        diagnostics.Start(logger, "old-peer");
        diagnostics.Record(ReceivePathEvent.UdpForwardFailed, 42, SocketError.ConnectionRefused);
        diagnostics.End("closed");
        var oldLifetime = sink.Events.Last().Properties["Lifetime"];
        diagnostics.Start(logger, "new-peer");
        diagnostics.Record(ReceivePathEvent.UdpForwarded, 8);
        var latest = sink.Events.Last();
        Assert.NotEqual(oldLifetime, latest.Properties["Lifetime"]);
        Assert.Contains("UdpForwardFailed\": 0", latest.RenderMessage());
        Assert.Contains("UdpForwarded\": 1", latest.RenderMessage());
        Assert.Contains("Success", latest.RenderMessage());
        Assert.DoesNotContain("old-peer", latest.RenderMessage());
    }

    [Fact]
    public void ConcurrentFailures_CountExactlyAndKeepFirstSocketErrorEvidenceBounded()
    {
        var sink = new CaptureSink();
        using var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        var diagnostics = new ReceivePathDiagnostics(() => 0, 100);
        diagnostics.Start(logger, "steam-host connection=7");
        Parallel.For(0, 10000, _ => diagnostics.Record(ReceivePathEvent.UdpForwardFailed, 3, SocketError.NoBufferSpaceAvailable));
        Assert.Equal(2, sink.Events.Count);
        Assert.Contains("NoBufferSpaceAvailable", sink.Events.Last().RenderMessage());
        diagnostics.End("stopped");
        Assert.Contains("UdpForwardFailed\": 10000", sink.Events.Last().RenderMessage());
        Assert.Contains("UdpForwardFailed\": 30000", sink.Events.Last().RenderMessage());
    }

    private sealed class CaptureSink : ILogEventSink
    {
        public ConcurrentQueue<LogEvent> Events { get; } = new();
        public void Emit(LogEvent logEvent) => Events.Enqueue(logEvent);
    }
}
