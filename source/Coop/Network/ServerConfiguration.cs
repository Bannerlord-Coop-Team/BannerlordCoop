using System;
using System.Net;

namespace Coop.Network
{
    public class ServerConfiguration
    {
        public TimeSpan keepAliveInterval = TimeSpan.FromSeconds(5);
        public IPAddress lanAddress = IPAddress.Parse("127.0.0.1");
        public TimeSpan lanDiscoveryInterval = TimeSpan.FromSeconds(2);
        public int lanPort = 4201;
        public uint uiMaxPlayerCount = 8;

        // statically initialized fields
        public uint uiTickRate = 120; // in [Hz]. 0 for no limit.

        // To be set during runtime
        public IPAddress wanAddress = null;
        public int wanPort = 4200;
    }
}
