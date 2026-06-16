using Common.Network;
using LiteNetLib;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Coop.Core.Common.Configuration;

/// <summary>
/// Network configuration used by the client and server
/// </summary>
public class NetworkConfig : INetworkConfig
{
#if DEBUG
    public string Address { get; set; } =  "localhost";
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromMinutes(5);
#else
    public string Address { get; set; } = "localhost";
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(10);
#endif

    public int Port { get; set; } = 4200;

    // TODO find better token
    public string Token { get; set; } = "TempToken";

    public string P2PToken => throw new NotImplementedException();

    public int MaxPacketsInQueue => 10000;

    // Resume threshold is well below the pause threshold so a chronically slow peer drains its backlog
    // before time resumes, instead of flapping pause/resume around a single limit (hysteresis).
    public int ResumePacketsInQueue => 5000;

    public TimeSpan AuditTimeout => TimeSpan.FromSeconds(15);

    public TimeSpan ObjectCreationTimeout => TimeSpan.FromSeconds(5);

    public TimeSpan NetworkPollInterval => TimeSpan.FromMilliseconds(50);

    #region MeshNetwork
    private string LanAddressText { get; set; } = "127.0.0.1";
    /// <summary>
    ///     ip address of the server in LAN.
    /// </summary>
    public IPAddress LanAddress => IPAddress.Parse(LanAddressText);
    /// <summary>
    ///     port of the server in LAN.
    /// </summary>
    public int LanPort { get; private set; } = 4201;

    private string WanAddressText { get; set; } = "144.202.53.18";
    /// <summary>
    ///     ip address of the server in WAN.
    /// </summary>
    public IPAddress WanAddress => IPAddress.Parse(WanAddressText);
    /// <summary>
    ///     port of the server in WAN.
    /// </summary>
    public int WanPort { get; private set; } = 4200;

    /// <summary>
    ///     Interval in which the server will send out LAN discovery messages.
    /// </summary>
    public TimeSpan LanDiscoveryInterval { get; } = TimeSpan.FromSeconds(2);
    /// <summary>
    ///     Interval in which the server will send out KeepAlive packets.
    /// </summary>
    public TimeSpan PingInterval { get; } = TimeSpan.FromSeconds(1);
    /// <summary>
    ///     If a connection is inactive (no requests or response) for longer than this time
    ///     frame, it will be disconnected.
    /// </summary>
#if DEBUG
    public TimeSpan DisconnectTimeout { get; } = TimeSpan.FromSeconds(60);
#else
        public TimeSpan DisconnectTimeout { get; } = TimeSpan.FromSeconds(60);
#endif

#if DEBUG
    public NatAddressType NATType { get; } = NatAddressType.Internal;
#else
        public NatAddressType NATType { get; } = NatAddressType.External;
#endif


    /// <summary>
    ///     Delay after a failed connection attempt until it is tried again.
    /// </summary>
    public TimeSpan ReconnectDelay { get; } = TimeSpan.FromSeconds(1);
    /// <summary>
    ///     Update cycle time for the network receiver.
    /// </summary>
    public TimeSpan UpdateTime { get; } = TimeSpan.FromMilliseconds(15);

    /// <summary>
    ///     Point the NAT-punch rendezvous at a specific endpoint (e.g. the co-hosting Coop
    ///     server) instead of the compiled-in defaults. Sets both LAN and WAN so the punch
    ///     reaches the target regardless of <see cref="NATType"/>.
    /// </summary>
    public void SetRendezvous(string address, int port)
    {
        // LanAddress/WanAddress parse the stored text with IPAddress.Parse, which rejects
        // hostnames like "localhost". Resolve to a numeric IP up front (preferring IPv4) so the
        // getters never throw and LiteNetLib gets a usable address.
        string resolved = ResolveToIp(address);
        LanAddressText = resolved;
        WanAddressText = resolved;
        LanPort = port;
        WanPort = port;
    }

    private static string ResolveToIp(string address)
    {
        if (string.IsNullOrWhiteSpace(address)) return address;
        if (IPAddress.TryParse(address, out _)) return address;

        try
        {
            var addresses = Dns.GetHostAddresses(address);
            var ipv4 = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            if (ipv4 != null) return ipv4.ToString();

            var any = addresses.FirstOrDefault();
            if (any != null) return any.ToString();
        }
        catch
        {
            // Fall through: keep the original text (will surface as a parse error if unusable).
        }

        return address;
    }
    #endregion
}
