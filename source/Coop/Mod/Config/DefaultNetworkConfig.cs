using Coop.NetImpl.LiteNet;
using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.Mod.Config
{
    public class DefaultNetworkConfig : INetworkConfig
    {
        public string ServerAddress { get; set; } = "localhost";
        public int ServerPort { get; set; } = 50352;
        public string ServerClientToken { get; set; } = "ServerClientToken";
        public string P2PHostAddress { get; set; }
        public int P2PPort { get; set; } = 50353;
        public string P2PToken { get; set; } = "P2PToken";
        public NatAddressType NATType { get; set; } = NatAddressType.External;
    }
}
