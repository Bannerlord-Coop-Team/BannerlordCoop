using Common.Network;
using System;
using System.Net;

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
    public string Address { get; set; } = "localhost";
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(10);
#endif

    public int Port { get; set; } = 4200;

    // TODO find better token
    public string Token { get; set; } = "TempToken";

    public string P2PToken => throw new NotImplementedException();

    public int MaxPacketsInQueue => 1000;

    public TimeSpan AuditTimeout => TimeSpan.FromSeconds(15);

    public TimeSpan ObjectCreationTimeout => TimeSpan.FromSeconds(5);

    public TimeSpan NetworkPollInterval => TimeSpan.FromMilliseconds(50);
}
