using LiteNetLib;
using System;
using System.Net;

namespace IntroServer.Config
{
    /// <summary>
    ///     Network settings for both client & server.
    /// </summary>
    public class NetworkConfiguration
    {
        /// <summary>
        ///     ip address of the server in LAN.
        /// </summary>
        public IPAddress LanAddress { get; } = IPAddress.Parse("127.0.0.1");
        /// <summary>
        ///     port of the server in LAN.
        /// </summary>
        public int LanPort { get; } = 4201;

        private string WanAddressText { get; set; } = "144.202.53.18";
        /// <summary>
        ///     ip address of the server in WAN.
        /// </summary>
        public IPAddress WanAddress => IPAddress.Parse(WanAddressText);
        /// <summary>
        ///     port of the server in WAN.
        /// </summary>
        public int WanPort { get; } = 4200;
        /// <summary>
        ///     Interval in which the server will send out LAN discovery messages.
        /// </summary>
        public TimeSpan LanDiscoveryInterval { get; } = TimeSpan.FromSeconds(2);
        /// <summary>
        ///     port the server will broadcast a LAN discovery message.
        /// </summary>
        public int LanDiscoveryPort { get; } = 4202;
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
        /// <summary>
        ///     Delay after a failed connection attempt until it is tried again.
        /// </summary>
        public TimeSpan ReconnectDelay { get; } = TimeSpan.FromSeconds(1);
        /// <summary>
        ///     Update cycle time for the network receiver.
        /// </summary>
        public TimeSpan UpdateTime { get; } = TimeSpan.FromMilliseconds(15);

#region P2P
        /// <summary>
        ///     P2P Identifier.
        /// </summary>
        public string P2PToken { get; } = "P2PToken";
        /// <summary>
        ///     P2P NAT Type
        /// </summary>
#if DEBUG
        public NatAddressType NATType { get; } = NatAddressType.Internal;
#else
        public NatAddressType NATType { get; } = NatAddressType.External;
#endif
        #endregion
    }
}
