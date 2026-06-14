using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Messages;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Instances.Messages;
using LiteNetLib;
using Serilog;

namespace Coop.Core.Server.Services.Instances.Handlers;

/// <summary>
/// Server-side bridge for P2P instance coordination. Assigns a server-issued instance id when a
/// client enters an interior location, and releases membership (re-electing the host) when a
/// client returns to the campaign or disconnects.
/// </summary>
public class ServerInstanceHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServerInstanceHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IInstanceCoordinator coordinator;

    public ServerInstanceHandler(IMessageBroker messageBroker, INetwork network, IInstanceCoordinator coordinator)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.coordinator = coordinator;

        messageBroker.Subscribe<NetworkEnterLocation>(Handle_EnterLocation);
        messageBroker.Subscribe<NetworkPlayerCampaignEntered>(Handle_CampaignEntered);
        messageBroker.Subscribe<PlayerDisconnected>(Handle_Disconnected);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkEnterLocation>(Handle_EnterLocation);
        messageBroker.Unsubscribe<NetworkPlayerCampaignEntered>(Handle_CampaignEntered);
        messageBroker.Unsubscribe<PlayerDisconnected>(Handle_Disconnected);
    }

    private void Handle_EnterLocation(MessagePayload<NetworkEnterLocation> payload)
    {
        var peer = (NetPeer)payload.Who;
        var data = payload.What;

        var result = coordinator.Join(peer, data.SettlementId, data.LocationId);

        network.Send(peer, new NetworkAssignInstance(
            result.InstanceId.ToString(), data.SettlementId, data.LocationId, result.BecameHost));

        Logger.Debug("Assigned instance {InstanceId} to peer (host={IsHost})", result.InstanceId, result.BecameHost);
    }

    private void Handle_CampaignEntered(MessagePayload<NetworkPlayerCampaignEntered> payload)
    {
        ReleaseMembership((NetPeer)payload.Who);
    }

    private void Handle_Disconnected(MessagePayload<PlayerDisconnected> payload)
    {
        ReleaseMembership(payload.What.PlayerId);
    }

    private void ReleaseMembership(NetPeer peer)
    {
        var result = coordinator.Leave(peer);
        if (result.WasMember == false) return;

        if (result.NewHost != null)
        {
            network.Send(result.NewHost, new NetworkInstanceHostChanged(result.InstanceId.ToString(), isHost: true));
            Logger.Debug("Migrated host of instance {InstanceId}", result.InstanceId);
        }
    }
}
