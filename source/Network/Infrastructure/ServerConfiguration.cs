using System;

namespace Network.Infrastructure
{
    public class ServerConfiguration
    {
        public ServerConfiguration()
        {
            // Initialize the UpdateTime to be roughly once per server tick.
            NetworkConfiguration = new NetworkConfiguration()
            {
                UpdateTime = TimeSpan.FromMilliseconds(1000 / (float) TickRate)
            };
        }
        public uint MaxPlayerCount { get; set; } = 8;
        public uint TickRate { get; set; } = 60; // in [Hz]. 0 for no limit.
        
        public NetworkConfiguration NetworkConfiguration { get; set; }
        
        /// <summary>
        ///     Defines the maximum amount of time after which an event will be dropped if it was not yet
        ///     delivered successfully. Do not set this too low, as the time it takes to transfer any arguments
        ///     is included in this.
        /// </summary>
        public TimeSpan EventBroadcastTimeout = TimeSpan.FromSeconds(20);
    }
}
