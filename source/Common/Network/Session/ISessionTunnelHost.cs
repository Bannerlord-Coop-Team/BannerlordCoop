using Common.Logging;
using Common.Network;
using Serilog;
using System;
using System.Net;

namespace Common.Network.Session;

/// <summary>
/// Shared advertise-time gate: a lobby must never claim a tunnel that is not listening,
/// and the tunnel may only start when the advertising client is connected to a local
/// server, because the host pump forwards to loopback.
/// </summary>
public static class TunnelAdvertisement
{
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(TunnelAdvertisement));

    public static void StartAndStamp(ISessionTunnelHost tunnelHost, INetworkConfig config, SessionJoinInfo info)
    {
        // A tunneled joiner's loopback address is its own join pump, not a local server,
        // so it must never host a tunnel of its own.
        if (!config.IsTunneled && IsLoopbackAddress(config.Address))
        {
            try
            {
                tunnelHost.Start(config.Port);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Session tunnel host failed to start; the lobby will be advertised direct-only");
            }
        }

        if (!tunnelHost.IsListening)
        {
            // Advertise as a pre-tunnel lobby so joiners use the address instead of
            // connecting to a tunnel that isn't there.
            info.Version = SessionJoinInfo.MinTunnelVersion - 1;
        }
    }

    public static bool IsLoopbackAddress(string address)
    {
        return address == "localhost" ||
            (IPAddress.TryParse(address, out var ip) && IPAddress.IsLoopback(ip));
    }
}

/// <summary>
/// Hosts the joiner-facing end of a session tunnel on the hosting player's client: remote
/// peers connect through a relay transport (a Steam P2P listen socket; no-op for plain
/// direct-IP hosting) and their datagrams are forwarded to the local server port.
/// </summary>
public interface ISessionTunnelHost : IDisposable
{
    bool IsListening { get; }

    /// <summary>Remote peers currently connected through the tunnel.</summary>
    int PeerCount { get; }

    /// <summary>
    /// Starts listening and forwards each connected peer's datagrams to the local server
    /// port. Safe to call again while listening.
    /// </summary>
    void Start(int serverPort);

    void Stop();
}
