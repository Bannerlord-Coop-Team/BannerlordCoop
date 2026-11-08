using Common.Logging;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Missions.Services.Network;

internal enum SendPathOp
{
    SendAll = 0,
    SendAllBut = 1,
    Send = 2,
}

// TEMPORARY (#4495 measurement rule): live counters around the mission message send entry points.
// Removed before the PR leaves draft.
internal static class SendPathCounters
{
    private const double ReportSeconds = 10;
    private static readonly ILogger Logger = LogManager.GetLogger<LiteNetP2PClient>();
    private static readonly string[] OpNames = { "SendAll", "SendAllBut", "Send" };

    private static readonly long[] calls = new long[3];
    private static readonly long[] ticks = new long[3];
    private static readonly long[] controllers = new long[3];
    private static readonly long[] serializations = new long[3];
    private static readonly long[] serializedBytes = new long[3];
    private static readonly ConcurrentDictionary<Type, long[]> perType = new();
    private static long windowStart = Stopwatch.GetTimestamp();

    [ThreadStatic] private static int depth;
    [ThreadStatic] private static int currentOp;
    [ThreadStatic] private static int lastPayloadBytes;

    public static long Begin(SendPathOp op)
    {
        if (depth++ != 0) return 0;
        currentOp = (int)op;
        lastPayloadBytes = 0;
        return Stopwatch.GetTimestamp();
    }

    public static void Serialized(int bytes, bool payload = true)
    {
        if (depth == 0) return;
        Interlocked.Increment(ref serializations[currentOp]);
        Interlocked.Add(ref serializedBytes[currentOp], bytes);
        if (payload) lastPayloadBytes = bytes;
    }

    public static void End(long start, Type messageType, int controllerCount)
    {
        if (--depth != 0) return;
        long elapsed = Stopwatch.GetTimestamp() - start;
        int op = currentOp;
        Interlocked.Increment(ref calls[op]);
        Interlocked.Add(ref ticks[op], elapsed);
        Interlocked.Add(ref controllers[op], controllerCount);
        long[] typeStats = perType.GetOrAdd(messageType, _ => new long[2]);
        Interlocked.Increment(ref typeStats[0]);
        Interlocked.Add(ref typeStats[1], lastPayloadBytes);
        MaybeReport();
    }

    private static void MaybeReport()
    {
        long now = Stopwatch.GetTimestamp();
        long started = Interlocked.Read(ref windowStart);
        double seconds = (double)(now - started) / Stopwatch.Frequency;
        if (seconds < ReportSeconds) return;
        if (Interlocked.CompareExchange(ref windowStart, now, started) != started) return;

        var parts = new string[3];
        long totalSerializations = 0;
        long totalBytes = 0;
        double totalMs = 0;
        for (int op = 0; op < 3; op++)
        {
            long opCalls = Interlocked.Exchange(ref calls[op], 0);
            double opMs = Interlocked.Exchange(ref ticks[op], 0) * 1000.0 / Stopwatch.Frequency;
            long opControllers = Interlocked.Exchange(ref controllers[op], 0);
            long opSerializations = Interlocked.Exchange(ref serializations[op], 0);
            long opBytes = Interlocked.Exchange(ref serializedBytes[op], 0);
            totalSerializations += opSerializations;
            totalBytes += opBytes;
            totalMs += opMs;
            parts[op] = string.Format(
                "{0} calls/s={1:0.0} ser/s={2:0.0} ser/call={3:0.00} ms/s={4:0.000} ns/call={5:0} controllers/call={6:0.00}",
                OpNames[op],
                opCalls / seconds,
                opSerializations / seconds,
                opCalls == 0 ? 0 : (double)opSerializations / opCalls,
                opMs / seconds,
                opCalls == 0 ? 0 : opMs * 1e6 / opCalls,
                opCalls == 0 ? 0 : (double)opControllers / opCalls);
        }

        string types = string.Join(", ", perType
            .Select(pair => (Name: pair.Key.Name, Count: Interlocked.Exchange(ref pair.Value[0], 0), Bytes: Interlocked.Exchange(ref pair.Value[1], 0)))
            .Where(stat => stat.Count > 0)
            .OrderByDescending(stat => stat.Count)
            .Take(6)
            .Select(stat => string.Format("{0} {1:0.0}/s {2:0}B", stat.Name, stat.Count / seconds, (double)stat.Bytes / stat.Count)));

        Logger.Information(
            "[SendPathCounters] window={Seconds:0.0}s total ser/s={Serializations:0.0} serKB/s={KB:0.0} ms/s={Ms:0.000} | {SendAll:l} | {SendAllBut:l} | {Send:l} | top: {Types:l}",
            seconds,
            totalSerializations / seconds,
            totalBytes / 1024.0 / seconds,
            totalMs / seconds,
            parts[0],
            parts[1],
            parts[2],
            types);
    }
}
