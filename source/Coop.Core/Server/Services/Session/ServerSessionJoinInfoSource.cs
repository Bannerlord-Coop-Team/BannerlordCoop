using Common;
using Common.Network;
using Common.Network.Session;
using Coop.Core.Common.Configuration;
using Coop.Steam;

namespace Coop.Core.Server.Services.Session;

/// <summary>
/// Builds standalone join metadata from the game-server identity, detected or configured public
/// address, mod build, and password-required flag.
/// </summary>
public class ServerSessionJoinInfoSource : ISessionJoinInfoSource
{
    private readonly SessionAdvertisementConfig advertisementConfig;
    private readonly INetworkConfig networkConfig;

    public ServerSessionJoinInfoSource(SessionAdvertisementConfig advertisementConfig, INetworkConfig networkConfig)
    {
        this.advertisementConfig = advertisementConfig;
        this.networkConfig = networkConfig;
    }

    public SessionJoinInfo Get()
    {
        // The Steam-detected public IP seeds the direct-connect fallback, but an explicitly
        // configured address wins (a detected IP is not always the reachable one).
        var address = string.IsNullOrEmpty(advertisementConfig.PublicAddress)
            ? SteamGameServerBoot.PublicIp
            : advertisementConfig.PublicAddress;

        return new SessionJoinInfo
        {
            Address = address,
            Port = networkConfig.Port,
            ServerSteamId = SteamGameServerBoot.GameServerSteamId,
            ModVersion = ModInformation.BuildVersion,
            PasswordRequired = !string.IsNullOrEmpty(networkConfig.Token),
        };
    }
}
