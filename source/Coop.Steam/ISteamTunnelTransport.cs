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

    bool SendDatagram(uint connection, byte[] data, int length);

    /// <summary>Reads one queued datagram into the buffer; returns its length, or 0 when none.</summary>
    int ReceiveDatagram(uint connection, byte[] buffer);
}
