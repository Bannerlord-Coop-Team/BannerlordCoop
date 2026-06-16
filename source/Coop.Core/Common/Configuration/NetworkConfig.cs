using Common.Network;
using System;
using System.Net;

namespace Coop.Core.Common.Configuration;

/// <summary>
/// Network configuration used by the client and server
/// </summary>
public class NetworkConfig : INetworkConfig
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

    public int MaxPacketsInQueue => 10000;

    // Resume threshold is well below the pause threshold so a chronically slow peer drains its backlog
    // before time resumes, instead of flapping pause/resume around a single limit (hysteresis).
    public int ResumePacketsInQueue => 5000;

    public TimeSpan AuditTimeout => TimeSpan.FromSeconds(15);

    public TimeSpan ObjectCreationTimeout => TimeSpan.FromSeconds(5);

    public TimeSpan NetworkPollInterval => TimeSpan.FromMilliseconds(50);
}
