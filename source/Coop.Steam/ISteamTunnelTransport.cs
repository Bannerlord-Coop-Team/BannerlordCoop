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
    public const int SendRateMinBytesPerSecond = 2 * 1024 * 1024;

    /// <summary>Ceiling for Steam's send pacing, headroom above the floor.</summary>
    public const int SendRateMaxBytesPerSecond = 20 * 1024 * 1024;

    /// <summary>
    /// Kernel buffers on the pump sockets. Must absorb what LiteNetLib keeps in flight
    /// while a full Steam send buffer parks the pump, or the kernel silently drops there.
    /// </summary>
    public const int LoopbackBufferBytes = 2 * 1024 * 1024;

    /// <summary>
    /// Classifies LiteNetLib's low-five-bit packet property. Unreliable, Ping, and Pong may be
    /// dropped under pressure; channeled traffic, acks, handshakes, and unknown values stay reliable.
    /// </summary>
    public static bool IsDroppableDatagram(byte[] data, int length)
    {
        if (length < 1) return true;

        int property = data[0] & 0x1F;
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
}

/// <summary>Optional authenticated-identity capability for a Steam tunnel transport.</summary>
public interface ISteamTunnelConnectionIdentityResolver
{
    bool TryGetRemoteSteamId(uint connection, out ulong steamId);
}
