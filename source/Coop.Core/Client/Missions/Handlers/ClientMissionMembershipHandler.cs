using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Missions;
using GameInterface.Missions.Messages;
using GameInterface.Services.Entity;
using GameInterface.Services.Locations.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;

namespace Coop.Core.Client.Missions.Handlers;

/// <summary>
/// Client → server relay membership. When the local player enters or leaves a location mission, this
/// announces it to the server over the campaign connection as <see cref="NetworkMissionEntered"/> /
/// <see cref="NetworkMissionLeft"/> so the server can map this controller to its connection for the relay
/// fallback. The instance id comes from <see cref="LocationInstanceId"/> — the same ObjectManager-id
/// derivation the P2P punch uses (see CoopTavernsController), so both land in one server-side instance.
/// </summary>
public class ClientMissionMembershipHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ClientMissionMembershipHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IControllerIdProvider controllerIdProvider;

    // The id sent on enter, replayed on leave so MissionLeft always matches its MissionEntered even if the
    // location objects are no longer resolvable at mission teardown. One location at a time per client.
    private string currentInstanceId;

    public ClientMissionMembershipHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IControllerIdProvider controllerIdProvider)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.controllerIdProvider = controllerIdProvider;

        //messageBroker.Subscribe<PlayerEnteredLocation>(Handle_PlayerEnteredLocation);
        //messageBroker.Subscribe<PlayerLeftLocation>(Handle_PlayerLeftLocation);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerEnteredLocation>(Handle_PlayerEnteredLocation);
        messageBroker.Unsubscribe<PlayerLeftLocation>(Handle_PlayerLeftLocation);
    }

    private void Handle_PlayerEnteredLocation(MessagePayload<PlayerEnteredLocation> payload)
    {
        var data = payload.What;

        if (LocationInstanceId.TryDerive(objectManager, data.Settlement, data.Location, out var instanceId) == false)
        {
            Logger.Warning("[Relay] Could not derive instance id for entered location — not announcing membership");
            return;
        }

        currentInstanceId = instanceId;
        network.SendAll(new NetworkMissionEntered(controllerIdProvider.ControllerId, instanceId));
        Logger.Information("[Relay] Announced MissionEntered for instance {Instance}", instanceId);
    }

    private void Handle_PlayerLeftLocation(MessagePayload<PlayerLeftLocation> payload)
    {
        if (currentInstanceId == null) return;

        network.SendAll(new NetworkMissionLeft(controllerIdProvider.ControllerId, currentInstanceId));
        Logger.Information("[Relay] Announced MissionLeft for instance {Instance}", currentInstanceId);
        currentInstanceId = null;
    }
}
