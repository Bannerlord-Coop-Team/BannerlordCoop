using Common.Network;
using System;

namespace Coop.Core.Common.Configuration;

/// <summary>
/// Network configuration used by the client and server
/// </summary>
public class NetworkConfiguration : INetworkConfiguration
{
#if DEBUG
    public string Address => "localhost";
#else
    public string Address => "bannerlordcoop.duckdns.org";
#endif

    public int Port => 4200;

    // TODO find better token
    public string Token => "TempToken";

    public string P2PToken => throw new NotImplementedException();

    public void LoadFromFile()
    {
        throw new NotImplementedException();
    }
}
