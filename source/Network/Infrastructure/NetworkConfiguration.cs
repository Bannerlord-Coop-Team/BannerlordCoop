using System;
using System.Net;

namespace Network.Infrastructure
{
    public class NetworkConfiguration
    {
        public IPAddress LanAddress { get; set; } = IPAddress.Parse("127.0.0.1");
        public int LanPort { get; set; } = 4201;
        public IPAddress WanAddress { get; set; } = null;
        public int WanPort { get; set; } = 4200;
        public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(1);
        public TimeSpan LanDiscoveryInterval { get; } = TimeSpan.FromSeconds(2);
        public TimeSpan DisconnectTimeout { get; set; } = TimeSpan.FromSeconds(5);
    }
}
