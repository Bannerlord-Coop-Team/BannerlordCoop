using Common.Network;
using System;

namespace Coop.Core.Common.Configuration;

/// <summary>
/// Network configuration used by the client and server
/// </summary>
public class NetworkConfiguration : INetworkConfiguration
{
#if DEBUG
    public string Address { get; set; } =  "localhost";
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromMinutes(5);
#else
    public string Address { get; set; } = "bannerlordcoop.duckdns.org";
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(10);
#endif

    public int Port { get; set; } = 4200;

    // TODO find better token
    public string Token { get; set; } = "TempToken";

    public string P2PToken => throw new NotImplementedException();

    public int MaxPacketsInQueue => 1000;

    public void LoadFromFile()
    {
        throw new NotImplementedException();
    }
}
