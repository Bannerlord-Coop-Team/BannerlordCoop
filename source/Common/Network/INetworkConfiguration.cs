using System;
using System.Collections.Generic;
using System.Text;

namespace Common.Network
{
    public interface INetworkConfiguration
    {
        string Address { get; }
        int Port { get; }
        string Token { get; }
        string P2PToken { get; }
    }
}
