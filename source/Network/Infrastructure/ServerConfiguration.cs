using System;

namespace Network.Infrastructure
{
    public class ServerConfiguration
    {
        public NetworkConfiguration NetworkConfiguration { get; set; } = new NetworkConfiguration();
        public uint MaxPlayerCount { get; set; } = 8;
        public uint TickRate { get; set; } = 120; // in [Hz]. 0 for no limit.
        
        /// <summary>
        ///     Defines the maximum amount of time after which an event will be dropped if it was not yet
        ///     delivered successfully. Do not set this too low, as the time it takes to transfer any arguments
        ///     is included in this.
        /// </summary>
        public TimeSpan EventBroadcastTimeout = TimeSpan.FromSeconds(20);
    }
}
