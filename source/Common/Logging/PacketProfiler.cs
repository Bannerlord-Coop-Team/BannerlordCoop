using Common.PacketHandlers;
using Common.Util;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

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
public sealed class PacketProfiler : IDisposable
{
    // A logger to dump the packet profile.
    private static readonly ILogger Logger = LogManager.GetLogger<PacketProfiler>();

    // A task to periodically dump the accumulated stats.
    private readonly Poller poller;

    private readonly PacketProfileAccumulator stats = new PacketProfileAccumulator();

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

        stats.Record(PacketProfileKey.FromPacket(packet), byteSize);
    }

    // Dumps the accumulated stats and clears them for the next window.
    private void Poll(TimeSpan dt)
    {
        var snapshot = stats.Drain();
        if (snapshot == null) return;

        // Order by bytes sent (largest first) and format each entry as a friendly line. A list is
        // rendered in order by Serilog (a Dictionary's key order is not preserved by the sinks).
        var ordered = snapshot
            .GroupBy(entry => entry.Key.Name)
            .Select(group => new
            {
                Name = group.Key,
                PacketsSent = group.Sum(entry => entry.Value.PacketsSent),
                BytesSent = group.Sum(entry => entry.Value.BytesSent)
            })
            .OrderByDescending(entry => entry.BytesSent)
            .Select(entry => $"{entry.Name}: {entry.PacketsSent} packets, {entry.BytesSent:N0} bytes")
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

    /// <summary>
    /// Disposes of the PacketProfiler, stopping the periodic dump.
    /// </summary>
    public void Dispose()
    {
        poller.Stop();
    }
}

/// <summary>Accumulates packet totals and atomically separates reporting windows.</summary>
internal sealed class PacketProfileAccumulator
{
    private readonly object gate = new object();
    private Dictionary<PacketProfileKey, PacketProfileStats> stats =
        new Dictionary<PacketProfileKey, PacketProfileStats>();

    public void Record(PacketProfileKey key, int byteSize)
    {
        lock (gate)
        {
            if (!stats.TryGetValue(key, out var existing))
            {
                existing = new PacketProfileStats();
                stats.Add(key, existing);
            }

            existing.Add(byteSize);
        }
    }

    public Dictionary<PacketProfileKey, PacketProfileStats> Drain()
    {
        lock (gate)
        {
            if (stats.Count == 0) return null;

            var snapshot = stats;
            stats = new Dictionary<PacketProfileKey, PacketProfileStats>();
            return snapshot;
        }
    }
}

/// <summary>Identifies either a packet type or one wrapped message type without formatting strings.</summary>
internal readonly struct PacketProfileKey : IEquatable<PacketProfileKey>
{
    private readonly Type packetType;
    private readonly Type messageType;

    public PacketProfileKey(Type packetType, Type messageType)
    {
        if (packetType == null) throw new ArgumentNullException(nameof(packetType));

        this.packetType = packetType;
        this.messageType = messageType;
    }

    public string Name
    {
        get
        {
            var packetName = packetType.Name;
            return messageType == null
                ? packetName
                : $"{packetName}:{GetFriendlyTypeName(messageType)}";
        }
    }

    public static PacketProfileKey FromPacket(IPacket packet)
    {
        if (packet == null) throw new ArgumentNullException(nameof(packet));

        // Keep wrapped messages in separate buckets without formatting their names on every send.
        var messageType = packet is MessagePacket messagePacket ? messagePacket.MessageType : null;
        return new PacketProfileKey(packet.GetType(), messageType);
    }

    public bool Equals(PacketProfileKey other) =>
        packetType == other.packetType && messageType == other.messageType;

    public override bool Equals(object obj) => obj is PacketProfileKey other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return (packetType.GetHashCode() * 397) ^ (messageType?.GetHashCode() ?? 0);
        }
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
}

/// <summary>Running packet count and serialized-byte total for one profile key.</summary>
internal sealed class PacketProfileStats
{
    public long PacketsSent { get; private set; }
    public long BytesSent { get; private set; }

    public void Add(int byteSize)
    {
        PacketsSent++;
        BytesSent += byteSize;
    }
}
