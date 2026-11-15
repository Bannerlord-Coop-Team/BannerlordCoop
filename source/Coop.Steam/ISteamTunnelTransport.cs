using Common.Network.Session;
using System;

namespace Coop.Steam;

/// <summary>
/// Shared constants for the Steam tunnel.
/// </summary>
public static class SteamTunnel
{
    /// <summary>Virtual port the host listens on and joiners connect to.</summary>
    public const int VirtualPort = 0;

    /// <summary>
    /// Upper bound for a tunneled datagram; comfortably above LiteNetLib's 1432-byte
    /// maximum packet size.
    /// </summary>
    public const int MaxDatagramBytes = 2048;

    /// <summary>
    /// Each pump pass adds at most this much one-way latency on top of the Steam link.
    /// </summary>
    public static readonly TimeSpan PumpInterval = TimeSpan.FromMilliseconds(2);

    /// <summary>
    /// Steam send buffer per tunnel connection. The join-save transfer bursts megabytes of
    /// fragments at once; the 512 KB default overflows and stalls the join.
    /// </summary>
    public const int SendBufferBytes = 8 * 1024 * 1024;

    /// <summary>
    /// Effective send-rate floor. This Steam build stays near its minimum while saturated, so the
    /// elevated floor keeps large join saves from taking minutes.
    /// </summary>
    public const int SendRateMinBytesPerSecond = 4 * 1024 * 1024;

    /// <summary>Ceiling for Steam's send pacing, headroom above the floor.</summary>
    public const int SendRateMaxBytesPerSecond = 20 * 1024 * 1024;

    /// <summary>Established connections may recover from a brief route interruption.</summary>
    public const int ConnectedTimeoutMilliseconds = 60 * 1000;

    /// <summary>
    /// Kernel buffers on the pump sockets. Must absorb what LiteNetLib keeps in flight
    /// while a full Steam send buffer parks the pump, or the kernel silently drops there.
    /// </summary>
    public const int LoopbackBufferBytes = 2 * 1024 * 1024;

    /// <summary>
    /// Classifies LiteNetLib's low-five-bit packet property. Unreliable, Ping, and Pong may be
    /// dropped under pressure; channeled traffic, acks, handshakes, and unknown values stay reliable.
    /// </summary>
    private static bool IsDroppableDatagram(byte[] data, int length)
    {
        if (length < 1) return true;

        return IsDroppableProperty(data[0]);
    }

    /// <summary>
    /// Copies a LiteNetLib datagram into one datagram per Steam lane; a lane with nothing to send gets
    /// length 0. Buffers must hold <paramref name="length"/> bytes.
    /// </summary>
    public static void SplitByLane(byte[] data, int length, byte[] droppable, out int droppableLength,
        byte[] reliable, out int reliableLength)
    {
        if (TrySplitMerged(data, length, droppable, out droppableLength, reliable, out reliableLength)) return;

        bool isDroppable = IsDroppableDatagram(data, length);
        Array.Copy(data, isDroppable ? droppable : reliable, length);
        droppableLength = isDroppable ? length : 0;
        reliableLength = isDroppable ? 0 : length;
    }

    // LiteNetLib packs the small packets a peer update queues into MTU-sized Merged datagrams,
    // unreliable movement included, so classifying only the outer byte carries them reliably.
    private const int MergedProperty = 12;

    // Re-frames a Merged datagram's inner packets as one Merged datagram per lane, keeping the
    // header byte; false for anything else or a malformed one, which the caller sends whole.
    private static bool TrySplitMerged(byte[] data, int length, byte[] droppable, out int droppableLength,
        byte[] reliable, out int reliableLength)
    {
        droppableLength = 0;
        reliableLength = 0;
        if (length < 1 || (data[0] & 0x1F) != MergedProperty) return false;

        droppable[0] = data[0];
        reliable[0] = data[0];
        int droppableEnd = 1;
        int reliableEnd = 1;
        int position = 1;
        while (position < length)
        {
            if (length - position < 2) return false;

            // Each inner packet is a little-endian ushort size followed by the packet itself.
            int size = data[position] | (data[position + 1] << 8);
            int framedSize = 2 + size;
            if (size == 0 || length - position < framedSize) return false;

            if (IsDroppableProperty(data[position + 2]))
            {
                Array.Copy(data, position, droppable, droppableEnd, framedSize);
                droppableEnd += framedSize;
            }
            else
            {
                Array.Copy(data, position, reliable, reliableEnd, framedSize);
                reliableEnd += framedSize;
            }

            position += framedSize;
        }

        droppableLength = droppableEnd > 1 ? droppableEnd : 0;
        reliableLength = reliableEnd > 1 ? reliableEnd : 0;
        return true;
    }

    // Unreliable, Ping and Pong in the low five bits of a LiteNetLib packet header.
    private static bool IsDroppableProperty(byte header)
    {
        int property = header & 0x1F;
        return property == 0 || property == 3 || property == 4;
    }
}

public enum TunnelConnectionState
{
    Connecting,
    Connected,
    Closed,
}

/// <summary>
/// Testable seam over Steam networking sockets. Implementations report only connections opened
/// through this instance or accepted by its listener.
/// </summary>
public interface ISteamTunnelTransport : IDisposable
{
    /// <summary>Raised from the Steam callback pump (the game thread) on state changes.</summary>
    event Action<uint, TunnelConnectionState> ConnectionStateChanged;

    /// <summary>Asks the relay network to warm up so the first connection doesn't pay for it.</summary>
    void EnsureRelayAccess();

    /// <summary>Opens a P2P connection to the host identity; returns the connection handle.</summary>
    uint ConnectToHost(ulong hostSteamId, int virtualPort);

    void ListenForClients(int virtualPort);

    void StopListening();

    void AcceptConnection(uint connection);

    void CloseConnection(uint connection);

    /// <summary>
    /// Queues a datagram. False asks reliable callers to retry after backpressure; droppable or
    /// dead-connection traffic is consumed so the pump cannot wedge.
    /// </summary>
    bool SendDatagram(uint connection, byte[] data, int length, bool droppable);

    /// <summary>Reads one queued datagram into the buffer; returns its length, or 0 when none.</summary>
    int ReceiveDatagram(uint connection, byte[] buffer);

    /// <summary>One-line live status (effective send rate, backlog, throughput, ping) for logs.</summary>
    string DescribeConnection(uint connection);

    /// <summary>Reads the connection's ping, delivery quality and unsent bytes; false when it has no status.</summary>
    bool TryGetConnectionHealth(uint connection, out RouteHealth health);
}

/// <summary>Optional authenticated-identity capability for a Steam tunnel transport.</summary>
public interface ISteamTunnelConnectionIdentityResolver
{
    bool TryGetRemoteSteamId(uint connection, out ulong steamId);
}
