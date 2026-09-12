using Common;
using Common.Network;
using Common.Network.Session;

namespace Coop.Core.Client.Services.Session;

/// <summary>
/// Builds the advertised join info for a client hosting a Steam tunnel to its local server.
/// </summary>
public class ConfiguredSessionJoinInfoSource : ISessionJoinInfoSource
{
    private readonly INetworkConfig networkConfig;

    public ConfiguredSessionJoinInfoSource(INetworkConfig networkConfig)
    {
        this.networkConfig = networkConfig;
    }

    public SessionJoinInfo Get()
    {
        return new SessionJoinInfo
        {
            Port = networkConfig.Port,
            ModVersion = ModInformation.BuildVersion,
            PasswordRequired = !string.IsNullOrEmpty(networkConfig.Token),
        };
    }
}
