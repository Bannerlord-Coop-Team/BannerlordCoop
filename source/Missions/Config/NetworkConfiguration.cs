using System;
using System.Net;

namespace Missions.Config
{
    public enum NATType
    {
        Internal,
        External,
    }

    /// <summary>
    ///     Network settings for both client & server.
    /// </summary>
    public class NetworkConfiguration
    {
        /// <summary>
        ///     ip address of the server in LAN.
        /// </summary>
        public IPAddress LanAddress { get; set; } = IPAddress.Parse("127.0.0.1");
        /// <summary>
        ///     port of the server in LAN.
        /// </summary>
        public int LanPort { get; set; } = 4201;
        /// <summary>
        ///     ip address of the server in WAN.
        /// </summary>
        public IPAddress WanAddress { get; set; } = IPAddress.Parse("127.0.0.1");
        /// <summary>
        ///     port of the server in WAN.
        /// </summary>
        public int WanPort { get; set; } = 4200;
        /// <summary>
        ///     Interval in which the server will send out LAN discovery messages.
        /// </summary>
        public TimeSpan LanDiscoveryInterval { get; } = TimeSpan.FromSeconds(2);
        /// <summary>
        ///     port the server will broadcast a LAN discovery message.
        /// </summary>
        public int LanDiscoveryPort { get; set; } = 4202;
        /// <summary>
        ///     Interval in which the server will send out <see cref="EPacket.KeepAlive"/>
        ///     packets.
        /// </summary>
        public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(1);
        /// <summary>
        ///     If a connection is inactive (no requests or response) for longer than this time
        ///     frame, it will be disconnected.
        /// </summary>
        public TimeSpan DisconnectTimeout { get; set; } = TimeSpan.FromSeconds(60);
        /// <summary>
        ///     Delay after a failed connection attempt until it is tried again.
        /// </summary>
        public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(2);
        /// <summary>
        ///     Update cycle time for the network receiver.
        /// </summary>
        public TimeSpan UpdateTime { get; set; } = TimeSpan.FromMilliseconds(15);

        #region P2P
        /// <summary>
        ///     P2P Identifier.
        /// </summary>
        public string P2PToken { get; set; } = "P2PToken";
        /// <summary>
        ///     P2P NAT Type
        /// </summary>
        public NATType NATType { get; set; } = NATType.Internal;
        #endregion
    }
}
