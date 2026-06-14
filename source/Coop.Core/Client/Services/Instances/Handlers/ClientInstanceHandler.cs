using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Network.Instances.Messages;
using Coop.Core.Server.Services.Instances.Messages;
using Serilog;

namespace Coop.Core.Client.Services.Instances.Handlers;

/// <summary>
/// Client-side bridge for P2P instance coordination. Translates between the local broker events
/// (consumed by the mission/P2P layer via <see cref="Common.Network.Instances.InstanceContext"/>)
/// and the over-the-wire instance messages exchanged with the authoritative server.
/// </summary>
public class ClientInstanceHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ClientInstanceHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;

    public ClientInstanceHandler(IMessageBroker messageBroker, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.network = network;

        // Local -> network
        messageBroker.Subscribe<EnterLocationRequested>(Handle_EnterLocationRequested);

        // Network -> local
        messageBroker.Subscribe<NetworkAssignInstance>(Handle_AssignInstance);
        messageBroker.Subscribe<NetworkInstanceHostChanged>(Handle_HostChanged);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<EnterLocationRequested>(Handle_EnterLocationRequested);
        messageBroker.Unsubscribe<NetworkAssignInstance>(Handle_AssignInstance);
        messageBroker.Unsubscribe<NetworkInstanceHostChanged>(Handle_HostChanged);
    }

    private void Handle_EnterLocationRequested(MessagePayload<EnterLocationRequested> payload)
    {
        var data = payload.What;
        network.SendAll(new NetworkEnterLocation(data.SettlementId, data.LocationId));
        Logger.Debug("Requested instance for {SettlementId}/{LocationId}", data.SettlementId, data.LocationId);
    }

    private void Handle_AssignInstance(MessagePayload<NetworkAssignInstance> payload)
    {
        var data = payload.What;
        messageBroker.Publish(this, new InstanceAssigned(data.InstanceId, data.IsHost, data.SettlementId, data.LocationId));
        Logger.Information("Assigned to instance {InstanceId} (host={IsHost})", data.InstanceId, data.IsHost);
    }

    private void Handle_HostChanged(MessagePayload<NetworkInstanceHostChanged> payload)
    {
        var data = payload.What;
        messageBroker.Publish(this, new InstanceHostChanged(data.InstanceId, data.IsHost));
    }
}
