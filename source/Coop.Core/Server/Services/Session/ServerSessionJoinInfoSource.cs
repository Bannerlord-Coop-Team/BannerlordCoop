using Common;
using Common.Network;
using Common.Network.Session;
using Coop.Steam;

namespace Coop.Core.Server.Services.Session;

/// <summary>
/// Builds standalone join metadata from the game-server identity, detected public address,
/// mod build, and password-required flag.
/// </summary>
public class ServerSessionJoinInfoSource : ISessionJoinInfoSource
{
    private readonly INetworkConfig networkConfig;

    public ServerSessionJoinInfoSource(INetworkConfig networkConfig)
    {
        this.networkConfig = networkConfig;
    }

    public SessionJoinInfo Get()
    {
        return new SessionJoinInfo
        {
            Address = SteamGameServerBoot.PublicIp,
            Port = networkConfig.Port,
            ServerSteamId = SteamGameServerBoot.GameServerSteamId,
            ModVersion = ModInformation.BuildVersion,
            PasswordRequired = !string.IsNullOrEmpty(networkConfig.Token),
        };
    }
}
