using Common;
using Common.Network;
using Common.Network.Session;

namespace Coop.Core.Client.Services.Session;

/// <summary>
/// Builds join metadata for a client hosting the active provider tunnel to its local server.
/// </summary>
public class ConfiguredSessionJoinInfoSource : ISessionJoinInfoSource
{
    private readonly INetworkConfig networkConfig;
    private readonly ISessionTransportTargetSource transportTargetSource;

    public ConfiguredSessionJoinInfoSource(
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
            Port = networkConfig.Port,
            TunnelTarget = transportTargetSource.TunnelTarget,
            ModVersion = ModInformation.BuildVersion,
            PasswordRequired = !string.IsNullOrEmpty(networkConfig.Token),
        };
    }
}
