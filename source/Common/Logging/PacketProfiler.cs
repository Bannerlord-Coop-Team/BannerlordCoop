using Common.PacketHandlers;
using Common.Util;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Common.Logging;

/// <summary>
/// Tallies outbound network packets and periodically dumps, per packet type, how many were sent
/// and how many bytes they totalled — to profile what dominates network traffic.
/// </summary>
/// <remarks>
/// Fed from the network send path (see <c>CoopNetworkBase.SendInternal</c>), so every recorded packet is
/// one actually sent over the wire, counted with its serialized byte size. A <see cref="MessagePacket"/>
/// is broken out by the message type it wraps (e.g. <c>MessagePacket:NetworkTroopRosterElementBatch</c>).
/// The accumulated stats are dumped on a fixed wall-clock interval. Only the server profiles traffic
/// (see <see cref="ModInformation.IsServer"/>).
/// </remarks>
public sealed class PacketProfiler : IDisposable, IPacketProfileCapture
{
    // A logger to dump the packet profile.
    private static readonly ILogger Logger = LogManager.GetLogger<PacketProfiler>();

    // A task to periodically dump the accumulated stats.
    private readonly Poller poller;

    private readonly ConcurrentDictionary<string, Stats> stats = new ConcurrentDictionary<string, Stats>();

    private readonly object captureLock = new object();
    private Capture capture;
    private Timer captureTimer;

    /// <summary>
    /// Optional provider of a one-line live-state summary (e.g. per-peer reliable-queue depth and ping)
    /// appended to each dump. Owned by the network layer, which is the only one that can see peers;
    /// the profiler itself stays free of any networking dependency.
    /// </summary>
    public Func<string> ExtraStatsProvider { get; set; }

    /// <summary>
    /// Constructs a PacketProfiler.
    /// </summary>
    /// <param name="dumpInterval">How often to dump the accumulated stats to the log.</param>
    public PacketProfiler(TimeSpan dumpInterval)
    {
        poller = new Poller(Poll, dumpInterval);
        poller.Start();
    }

    /// <summary>
    /// Records one packet sent over the network and its serialized size in bytes. No-op off the server.
    /// </summary>
    public void Record(IPacket packet, int byteSize)
    {
        // Only the server profiles network traffic.
        if (ModInformation.IsClient) return;

        var packetName = GetPacketName(packet);

        stats.AddOrUpdate(packetName, _ => new Stats(1, byteSize), (_, existing) => existing.Add(byteSize));
        RecordCapture(packetName, byteSize);
    }

    public bool TryStartCapture(
        string packetName,
        TimeSpan duration,
        Action completion,
        out PacketProfileCaptureSnapshot snapshot,
        out string error)
    {
        snapshot = null;
        error = null;
        if (string.IsNullOrWhiteSpace(packetName))
        {
            error = "Packet name is required.";
            return false;
        }
        if (duration <= TimeSpan.Zero)
        {
            error = "Capture duration must be positive.";
            return false;
        }

        lock (captureLock)
        {
            if (capture != null && !capture.Completed)
            {
                error = $"Capture '{capture.CaptureId}' is already running.";
                return false;
            }

            captureTimer?.Dispose();
            long startedTimestamp = Stopwatch.GetTimestamp();
            long durationTimestampTicks = (long)Math.Ceiling(duration.TotalSeconds * Stopwatch.Frequency);
            var startedUtc = DateTimeOffset.UtcNow;
            capture = new Capture(
                Guid.NewGuid().ToString("N"),
                packetName,
                duration,
                startedTimestamp,
                startedTimestamp + durationTimestampTicks,
                startedUtc,
                completion);
            captureTimer = new Timer(CompleteCaptureTimer, null, duration, Timeout.InfiniteTimeSpan);
            snapshot = capture.CreateSnapshot(startedTimestamp);
            return true;
        }
    }

    public bool TryGetCapture(out PacketProfileCaptureSnapshot snapshot, out string error)
    {
        lock (captureLock)
        {
            if (capture == null)
            {
                snapshot = null;
                error = "No packet-profile capture exists.";
                return false;
            }

            snapshot = capture.CreateSnapshot(Stopwatch.GetTimestamp());
            error = null;
            return true;
        }
    }

    public bool TryCancelCapture(out PacketProfileCaptureSnapshot snapshot, out string error)
    {
        Action completion;
        lock (captureLock)
        {
            if (capture == null)
            {
                snapshot = null;
                error = "No packet-profile capture exists.";
                return false;
            }

            completion = CompleteCaptureLocked(Stopwatch.GetTimestamp(), cancelled: true);
            snapshot = capture.CreateSnapshot(Stopwatch.GetTimestamp());
            error = null;
        }

        InvokeCompletion(completion);
        return true;
    }

    private void RecordCapture(string packetName, int byteSize)
    {
        Action completion = null;
        lock (captureLock)
        {
            if (capture == null || capture.Completed)
                return;

            long now = Stopwatch.GetTimestamp();
            if (now >= capture.EndTimestamp)
            {
                completion = CompleteCaptureLocked(now, cancelled: false);
            }
            else if (packetName == capture.PacketName)
            {
                capture.Record(byteSize);
            }
        }

        InvokeCompletion(completion);
    }

    private void CompleteCaptureTimer(object state)
    {
        Action completion;
        lock (captureLock)
        {
            completion = CompleteCaptureLocked(Stopwatch.GetTimestamp(), cancelled: false);
        }

        InvokeCompletion(completion);
    }

    private Action CompleteCaptureLocked(long completedTimestamp, bool cancelled)
    {
        if (capture == null || capture.Completed)
            return null;

        capture.Complete(completedTimestamp, DateTimeOffset.UtcNow, cancelled);
        captureTimer?.Dispose();
        captureTimer = null;
        Action completion = capture.Completion;
        capture.Completion = null;
        return completion;
    }

    private static void InvokeCompletion(Action completion)
    {
        if (completion == null)
            return;

        try
        {
            completion();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Packet-profile capture completion callback failed");
        }
    }

    // Dumps the accumulated stats and clears them for the next window.
    private void Poll(TimeSpan dt)
    {
        if (stats.IsEmpty) return;

        // Drain the stats into a snapshot, clearing the dictionary for the next window.
        var snapshot = new Dictionary<string, Stats>(stats.Count);
        foreach (var packetName in stats.Keys)
        {
            if (stats.TryRemove(packetName, out var packetStats))
            {
                snapshot[packetName] = packetStats;
            }
        }

        // Order by bytes sent (largest first) and format each entry as a friendly line. A list is
        // rendered in order by Serilog (a Dictionary's key order is not preserved by the sinks).
        var ordered = snapshot
            .OrderByDescending(entry => entry.Value.BytesSent)
            .Select(entry => $"{entry.Key}: {entry.Value.PacketsSent} packets, {entry.Value.BytesSent:N0} bytes")
            .ToList();

        // Average outbound throughput over the window: total bytes sent divided by the elapsed seconds.
        var totalBytes = snapshot.Values.Sum(s => s.BytesSent);
        var seconds = dt.TotalSeconds;
        var bytesPerSecond = seconds > 0 ? totalBytes / seconds : 0;

        Logger.Information(
            "Packet profile over {Seconds:0.#} seconds ({BytesPerSecond:N0} bytes/sec avg): {@PacketProfile}{ExtraStats}",
            seconds, bytesPerSecond, ordered, GetExtraStats());
    }

    // Never let a faulty provider kill the dump; the profile itself is the primary payload.
    private string GetExtraStats()
    {
        try
        {
            var extra = ExtraStatsProvider?.Invoke();
            return string.IsNullOrEmpty(extra) ? string.Empty : $" | {extra}";
        }
        catch (Exception ex)
        {
            return $" | peer stats unavailable: {ex.GetType().Name}";
        }
    }

    private static string GetPacketName(IPacket packet)
    {
        var packetName = packet.GetType().Name;

        // Break MessagePacket out by the message type it wraps so it is not one opaque bucket.
        if (packet is MessagePacket messagePacket && messagePacket.MessageType != null)
        {
            packetName += $":{GetFriendlyTypeName(messagePacket.MessageType)}";
        }

        return packetName;
    }

    private static string GetFriendlyTypeName(Type type)
    {
        if (!type.IsGenericType)
            return type.Name;

        var name = type.Name;
        var tickIndex = name.IndexOf('`');
        if (tickIndex > 0)
            name = name.Substring(0, tickIndex);

        var genericArgs = type.GetGenericArguments();
        var argNames = new string[genericArgs.Length];

        for (int i = 0; i < genericArgs.Length; i++)
        {
            argNames[i] = GetFriendlyTypeName(genericArgs[i]);
        }

        return $"{name}<{string.Join(", ", argNames)}>";
    }

    /// <summary>
    /// Disposes of the PacketProfiler, stopping the periodic dump.
    /// </summary>
    public void Dispose()
    {
        poller.StopAndWait(TimeSpan.FromSeconds(5));
        lock (captureLock)
        {
            captureTimer?.Dispose();
            captureTimer = null;
        }
    }

    private sealed class Capture
    {
        public string CaptureId { get; }
        public string PacketName { get; }
        public TimeSpan Duration { get; }
        public long StartedTimestamp { get; }
        public long EndTimestamp { get; }
        public DateTimeOffset StartedUtc { get; }
        public Action Completion { get; set; }
        public long PacketsSent { get; private set; }
        public long BytesSent { get; private set; }
        public bool Completed { get; private set; }
        public bool Cancelled { get; private set; }
        public long CompletedTimestamp { get; private set; }
        public DateTimeOffset? CompletedUtc { get; private set; }

        public Capture(
            string captureId,
            string packetName,
            TimeSpan duration,
            long startedTimestamp,
            long endTimestamp,
            DateTimeOffset startedUtc,
            Action completion)
        {
            CaptureId = captureId;
            PacketName = packetName;
            Duration = duration;
            StartedTimestamp = startedTimestamp;
            EndTimestamp = endTimestamp;
            StartedUtc = startedUtc;
            Completion = completion;
        }

        public void Record(int byteSize)
        {
            PacketsSent++;
            BytesSent += byteSize;
        }

        public void Complete(long completedTimestamp, DateTimeOffset completedUtc, bool cancelled)
        {
            Completed = true;
            Cancelled = cancelled;
            CompletedTimestamp = completedTimestamp;
            CompletedUtc = completedUtc;
        }

        public PacketProfileCaptureSnapshot CreateSnapshot(long now)
        {
            long elapsedTimestampTicks = Completed
                ? Math.Min(CompletedTimestamp, EndTimestamp) - StartedTimestamp
                : Math.Min(now, EndTimestamp) - StartedTimestamp;
            long elapsedMilliseconds = (long)Math.Round(
                elapsedTimestampTicks * 1000d / Stopwatch.Frequency,
                MidpointRounding.AwayFromZero);
            long durationMilliseconds = (long)Math.Round(Duration.TotalMilliseconds, MidpointRounding.AwayFromZero);

            return new PacketProfileCaptureSnapshot(
                CaptureId,
                Completed ? Cancelled ? "cancelled" : "completed" : "running",
                PacketName,
                PacketsSent,
                BytesSent,
                durationMilliseconds,
                elapsedMilliseconds,
                StartedUtc,
                StartedUtc + Duration,
                CompletedUtc,
                Cancelled);
        }
    }

    // Running per-type totals: how many packets were sent and their combined serialized byte size.
    private readonly struct Stats
    {
        public readonly long PacketsSent;
        public readonly long BytesSent;

        public Stats(long packetsSent, long bytesSent)
        {
            PacketsSent = packetsSent;
            BytesSent = bytesSent;
        }

        public Stats Add(int byteSize) => new Stats(PacketsSent + 1, BytesSent + byteSize);
    }
}
