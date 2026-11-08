using Common.Logging;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;

namespace Common.Network;

// TEMP #4488 measurement, removed before the PR leaves draft: cumulative reliable send counts and bytes per key,
// logged every 5 s so a bench window's rates come from the two lines around it.
public static class TrafficProbe4488
{
    private static readonly ILogger Logger = LogManager.GetLogger<TrafficCounters>();

    // LiteNetLib's initial MTU minus the channeled header; neither NetManager turns on MTU discovery.
    private const int MaxSinglePacketBytes = 1020;

    private static readonly ConcurrentDictionary<string, TrafficCounters> counters =
        new ConcurrentDictionary<string, TrafficCounters>();

    private static readonly Timer timer = new Timer(_ => Dump(), null, 5000, 5000);

    public static void Record(string key, int bytes)
    {
        TrafficCounters entry = counters.GetOrAdd(key, _ => new TrafficCounters());
        Interlocked.Increment(ref entry.Count);
        Interlocked.Add(ref entry.Bytes, bytes);
        if (bytes > MaxSinglePacketBytes) Interlocked.Increment(ref entry.Oversized);
    }

    private static void Dump()
    {
        try
        {
            if (counters.IsEmpty) return;
            var line = new StringBuilder();
            foreach (var pair in counters.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                line.Append(pair.Key).Append('=')
                    .Append(Interlocked.Read(ref pair.Value.Count)).Append('/')
                    .Append(Interlocked.Read(ref pair.Value.Bytes)).Append('/')
                    .Append(Interlocked.Read(ref pair.Value.Oversized)).Append(';');
            }
            Logger.Information("TEMP4488 utc={Utc} {Counters}", DateTime.UtcNow.ToString("o"), line.ToString());
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "TEMP4488 dump failed");
        }
    }

    private sealed class TrafficCounters
    {
        public long Count;
        public long Bytes;
        public long Oversized;
    }
}
