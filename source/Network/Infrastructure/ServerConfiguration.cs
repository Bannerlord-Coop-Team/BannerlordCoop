using System;
using System.Net;

namespace Network.Infrastructure
{
    public class ServerConfiguration
    {
        /// <summary>
        ///     Defines the maximum amount of time after which an event will be dropped if it was not yet
        ///     delivered successfully. Do not set this too low, as the time it takes to transfer any arguments
        ///     is included in this.
        /// </summary>
        public TimeSpan EventBroadcastTimeout = TimeSpan.FromSeconds(20);

        public IPAddress LanAddress { get; set; } = IPAddress.Parse("127.0.0.1");
        public int LanPort { get; set; } = 4201;
        public IPAddress WanAddress { get; set; } = null;
        public int WanPort { get; set; } = 4200;
        public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(1);
        public TimeSpan LanDiscoveryInterval { get; } = TimeSpan.FromSeconds(2);
        public TimeSpan DisconnectTimeout { get; set; } = TimeSpan.FromSeconds(5);
        public uint MaxPlayerCount { get; set; } = 8;
        public uint TickRate { get; set; } = 120; // in [Hz]. 0 for no limit.
    }
}
