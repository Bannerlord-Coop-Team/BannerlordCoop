using LiteNetLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coop.NetImpl.LiteNet
{
    public interface INetworkConfig
    {
        #region ServerClientArch
        string ServerAddress { get; set; }
        int ServerPort { get; set; }
        string ServerClientToken { get; set; }
        #endregion

        #region P2PArch
        string P2PHostAddress { get; set; }
        int P2PPort { get; set; }
        string P2PToken { get; set; }
        NatAddressType NATType { get; set; }
        #endregion
    }
}
