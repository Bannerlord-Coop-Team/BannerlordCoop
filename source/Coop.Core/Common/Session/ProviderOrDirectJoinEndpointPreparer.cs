using Common.Messaging;
using Common.Network.Session;
using Common.Network.Session.Messages;
using Coop.Core.Common.Services.Connection.Messages;
using System;
using System.Threading.Tasks;

namespace Coop.Core.Common.Session;

/// <summary>
/// Picks the join transport: the matching provider tunnel when the listing advertises one,
/// otherwise the direct address. Also ends the active tunnel
/// when the session ends or the join fails.
/// </summary>
public class ProviderOrDirectJoinEndpointPreparer : IJoinEndpointPreparer
{
    private readonly IJoinEndpointPreparer direct = new DirectJoinEndpointPreparer();

    public ProviderOrDirectJoinEndpointPreparer()
    {
        MessageBroker.Instance.Subscribe<EndCoopMode>(Handle_EndCoopMode);
        MessageBroker.Instance.Subscribe<SessionJoinFailed>(Handle_SessionJoinFailed);
    }

    public Task<SessionJoinInfo> PrepareAsync(SessionJoinInfo info)
    {
        var tunnelPreparer = SessionDiscovery.ClientProvider?.JoinEndpointPreparer;
        if (info.HasTunnelTarget &&
            tunnelPreparer != null &&
            string.Equals(
                tunnelPreparer.Provider,
                info.TunnelTarget.Provider,
                StringComparison.Ordinal))
        {
            return tunnelPreparer.PrepareAsync(info);
        }

        return direct.PrepareAsync(info);
    }

    /// <summary>Closes the active tunnel; for failure exits that publish no session message.</summary>
    public void TearDownActiveTunnel()
    {
        SessionDiscovery.ClientProvider?.JoinEndpointPreparer?.TearDown();
    }

    private void Handle_EndCoopMode(MessagePayload<EndCoopMode> payload)
    {
        TearDownActiveTunnel();
    }

    private void Handle_SessionJoinFailed(MessagePayload<SessionJoinFailed> payload)
    {
        TearDownActiveTunnel();
    }
}
