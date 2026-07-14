using Common;
using Common.Network.Session;
using Common.Network;
using Coop.Core.Common.Configuration;

namespace Coop.Core.Client.Services.Session;

/// <summary>
/// Builds the advertised join info from what the hosting player configured: their public
/// address (possibly empty) and the port their session runs on.
/// </summary>
public class ConfiguredSessionJoinInfoSource : ISessionJoinInfoSource
{
    private readonly SessionAdvertisementConfig advertisementConfig;
    private readonly INetworkConfig networkConfig;

    public ConfiguredSessionJoinInfoSource(SessionAdvertisementConfig advertisementConfig, INetworkConfig networkConfig)
    {
        this.advertisementConfig = advertisementConfig;
        this.networkConfig = networkConfig;
    }

    public SessionJoinInfo Get()
    {
        return new SessionJoinInfo
        {
            Address = advertisementConfig.PublicAddress,
            Port = networkConfig.Port,
            ModVersion = ModInformation.BuildVersion,
            PasswordRequired = !string.IsNullOrEmpty(networkConfig.Token),
        };
    }
}
