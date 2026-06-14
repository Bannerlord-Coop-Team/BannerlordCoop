using Common.PacketHandlers;
using Common.Util;
using Serilog;
using System;
using System.Collections.Concurrent;
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
/// is broken out by the message type it wraps (e.g. <c>MessagePacket:NetworkAddToCountsAtIndex</c>).
/// The accumulated stats are dumped on a fixed wall-clock interval. Only the server profiles traffic
/// (see <see cref="ModInformation.IsServer"/>).
/// </remarks>
public sealed class PacketProfiler : IDisposable
{
    // A logger to dump the packet profile.
    private static readonly ILogger Logger = LogManager.GetLogger<PacketProfiler>();

    // A task to periodically dump the accumulated stats.
    private readonly Poller poller;

    private readonly ConcurrentDictionary<string, Stats> stats = new ConcurrentDictionary<string, Stats>();

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

        Logger.Information("Packet profile over {Seconds:0.#} seconds: {@PacketProfile}", dt.TotalSeconds, ordered);
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
        poller.Stop();
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
