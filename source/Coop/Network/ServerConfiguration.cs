using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Network
{
    public class ServerConfiguration
    {
        // To be set during runtime
        public IPAddress wanAddress;
        public int wanPort;
        public IPAddress lanAddress;
        public int lanPort;

        // statically initialized fields
        public uint uiTickRate = 120; // in [Hz]. 0 for no limit.
        public uint uiMaxPlayerCount = 8;
        public TimeSpan wanKeepAliveInterval = TimeSpan.FromSeconds(5);
        public TimeSpan lanDiscoveryInterval = TimeSpan.FromSeconds(2);
    }
}
