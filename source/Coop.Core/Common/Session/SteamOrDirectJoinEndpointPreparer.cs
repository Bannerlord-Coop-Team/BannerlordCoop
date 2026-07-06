using Common.Messaging;
using Common.Network.Session;
using Common.Network.Session.Messages;
using Coop.Core.Common.Services.Connection.Messages;
using Coop.Steam;
using System.Threading.Tasks;

namespace Coop.Core.Common.Session;

/// <summary>
/// Picks the join transport: the Steam tunnel when the lobby advertised a tunnel-capable
/// host and Steam is available, otherwise the direct address. Also ends the active tunnel
/// when the session ends or the join fails.
/// </summary>
public class SteamOrDirectJoinEndpointPreparer : IJoinEndpointPreparer
{
    private readonly IJoinEndpointPreparer direct = new DirectJoinEndpointPreparer();

    public SteamOrDirectJoinEndpointPreparer()
    {
        MessageBroker.Instance.Subscribe<EndCoopMode>(Handle_EndCoopMode);
        MessageBroker.Instance.Subscribe<SessionJoinFailed>(Handle_SessionJoinFailed);
    }

    public Task<SessionJoinInfo> PrepareAsync(SessionJoinInfo info)
    {
        if (SessionDiscovery.SteamAvailable && info.HasHostSteamId && SteamBoot.TunnelPreparer != null)
        {
            return SteamBoot.TunnelPreparer.PrepareAsync(info);
        }

        return direct.PrepareAsync(info);
    }

    /// <summary>Closes the active tunnel; for failure exits that publish no session message.</summary>
    public void TearDownActiveTunnel()
    {
        SteamBoot.TunnelPreparer?.TearDown();
    }

    private void Handle_EndCoopMode(MessagePayload<EndCoopMode> payload)
    {
        SteamBoot.TunnelPreparer?.TearDown();
    }

    private void Handle_SessionJoinFailed(MessagePayload<SessionJoinFailed> payload)
    {
        SteamBoot.TunnelPreparer?.TearDown();
    }
}
