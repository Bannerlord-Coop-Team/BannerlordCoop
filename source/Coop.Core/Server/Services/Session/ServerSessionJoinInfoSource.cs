using Common;
using Common.Network;
using Common.Network.Session;

namespace Coop.Core.Server.Services.Session;

/// <summary>
/// Builds standalone join metadata from the game-server identity, detected public address,
/// mod build, and password-required flag.
/// </summary>
public class ServerSessionJoinInfoSource : ISessionJoinInfoSource
{
    private readonly INetworkConfig networkConfig;
    private readonly ISessionTransportTargetSource transportTargetSource;

    public ServerSessionJoinInfoSource(
        INetworkConfig networkConfig,
        ISessionTransportTargetSource transportTargetSource)
    {
        this.networkConfig = networkConfig;
        this.transportTargetSource = transportTargetSource;
    }

    public SessionJoinInfo Get()
    {
        return new SessionJoinInfo
        {
            Address = transportTargetSource.PublicAddress,
            Port = networkConfig.Port,
            TunnelTarget = transportTargetSource.TunnelTarget,
            DedicatedServer = true,
            ModVersion = ModInformation.BuildVersion,
            PasswordRequired = !string.IsNullOrEmpty(networkConfig.Token),
        };
    }
}
