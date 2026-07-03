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
    /// Send-pacing floor while a join-sized reliable backlog is draining. The pacer's
    /// estimate never rises on its own in the game's Steam build (observed pinned at the
    /// 256 KB/s default while saturated on a 3 ms link), so this floor is what carries the
    /// ~55 MB join payload in seconds instead of minutes; the tunnel host's governor
    /// applies it only under backlog and backs it off when delivery quality sags.
    /// </summary>
    public const int TransferFloorBytesPerSecond = 2 * 1024 * 1024;

    /// <summary>Ceiling for Steam's send pacing, headroom above the floor.</summary>
    public const int SendRateMaxBytesPerSecond = 20 * 1024 * 1024;

    /// <summary>
    /// Kernel buffers on the pump sockets. Must absorb what LiteNetLib keeps in flight
    /// while a full Steam send buffer parks the pump, or the kernel silently drops there.
    /// </summary>
    public const int LoopbackBufferBytes = 2 * 1024 * 1024;

    /// <summary>
    /// True when the datagram's sender tolerates loss, so the tunnel may keep it off the
    /// reliable Steam lane and drop it under pressure instead of stalling newer traffic.
    /// LiteNetLib 1.3.1 wire format: the packet property is the low five bits of the first
    /// byte; Unreliable (0), Ping (3), and Pong (4) are droppable by contract. Anything
    /// else — channeled traffic, acks, handshake, merged datagrams — and any unknown value
    /// is delivered reliably.
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
/// Minimal seam over the Steam networking-sockets surface the tunnel pumps use, so they can
/// be tested against a fake and a differently-flavored implementation can slot in later.
/// Implementations only raise <see cref="ConnectionStateChanged"/> for connections they own:
/// ones opened via <see cref="ConnectToHost"/> or arriving on <see cref="ListenForClients"/>.
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
    /// Queues one datagram for delivery. A droppable datagram may be discarded when the
    /// send buffer is full (its sender tolerates loss); otherwise false means the buffer is
    /// full and the caller must retry the same datagram later — never drop it. A dead
    /// connection swallows the datagram, so a retry loop can never wedge on it.
    /// </summary>
    bool SendDatagram(uint connection, byte[] data, int length, bool droppable);

    /// <summary>Reads one queued datagram into the buffer; returns its length, or 0 when none.</summary>
    int ReceiveDatagram(uint connection, byte[] buffer);

    /// <summary>One-line live status (effective send rate, backlog, throughput, ping) for logs.</summary>
    string DescribeConnection(uint connection);

    /// <summary>Sets the connection's send-pacing floor.</summary>
    void SetSendRateFloor(uint connection, int bytesPerSecond);

    /// <summary>
    /// Live send-side health; false when unavailable. Quality is the delivered fraction of
    /// our sends, negative until Steam has a measurement.
    /// </summary>
    bool TryGetSendHealth(uint connection, out int pendingReliableBytes, out float quality);
}
