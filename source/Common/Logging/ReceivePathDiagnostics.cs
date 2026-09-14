using Serilog;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Net.Sockets;

namespace Common.Logging;

public interface IReceivePathDiagnostics
{
    void Start(ILogger context, string correlation);
    void Record(ReceivePathEvent kind, int bytes = 0, SocketError error = SocketError.Success);
    void End(string reason);
}

/// <summary>One peer/route lifetime, with first evidence and cumulative, bounded traffic summaries.</summary>
public class ReceivePathDiagnostics : IReceivePathDiagnostics
{
    private readonly object gate = new object();
    private readonly Func<long> timestamp;
    private readonly long interval;
    private readonly long[] counts = new long[(int)ReceivePathEvent.Count];
    private readonly long[] bytes = new long[(int)ReceivePathEvent.Count];
    private ILogger logger;
    private string correlation;
    private Guid lifetime;
    private long lastEmission;
    private SocketError lastSocketError;
    private bool ended;

    public ReceivePathDiagnostics() : this(Stopwatch.GetTimestamp, Stopwatch.Frequency * 10) { }

    internal ReceivePathDiagnostics(Func<long> timestamp, long interval)
    {
        this.timestamp = timestamp;
        this.interval = interval;
    }

    public void Start(ILogger context, string correlation)
    {
        lock (gate)
        {
            logger = context;
            this.correlation = correlation;
            lifetime = Guid.NewGuid();
            Array.Clear(counts, 0, counts.Length);
            Array.Clear(bytes, 0, bytes.Length);
            lastSocketError = SocketError.Success;
            lastEmission = timestamp();
            ended = false;
            logger.Information("[ReceivePath] utc={Utc:O} scope={Scope} lifetime={Lifetime} started",
                DateTime.UtcNow, correlation, lifetime);
        }
    }

    public void Record(ReceivePathEvent kind, int bytes = 0, SocketError error = SocketError.Success)
    {
        lock (gate)
        {
            if (ended || logger == null) return;
            int index = (int)kind;
            counts[index]++;
            this.bytes[index] += bytes;
            if (error != SocketError.Success) lastSocketError = error;
            long now = timestamp();
            // Each fixed category gets one prompt sample; repeats share a ten-second budget.
            if (counts[index] != 1 && now - lastEmission < interval) return;
            lastEmission = now;
            Emit(kind.ToString());
        }
    }

    public void End(string reason)
    {
        lock (gate)
        {
            if (ended || logger == null) return;
            ended = true;
            Emit(reason);
        }
    }

    private void Emit(string reason)
    {
        var totals = new Dictionary<string, long>();
        var byteTotals = new Dictionary<string, long>();
        for (int i = 0; i < counts.Length; i++)
        {
            totals[((ReceivePathEvent)i).ToString()] = counts[i];
            byteTotals[((ReceivePathEvent)i).ToString()] = bytes[i];
        }
        logger.Information(
            "[ReceivePath] utc={Utc:O} scope={Scope} lifetime={Lifetime} reason={Reason} counts={@Counts} bytes={@Bytes} lastSocketError={SocketError}",
            DateTime.UtcNow, correlation, lifetime, reason, totals, byteTotals, lastSocketError);
    }
}

// Fixed categories bound both memory and first-evidence emissions.
public enum ReceivePathEvent
{
    MappedReceive,
    UnmappedDrop,
    MissionPeerSend,
    CampaignRelaySend,
    SteamReceive,
    UdpForwarded,
    UdpForwardFailed,
    NoEndpointDrop,
    UdpReceiveError,
    DisposedSocket,
    Count,
}
