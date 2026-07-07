using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.SiegeEngines.Messages;
using Coop.Core.Server.Services.SiegeEngines.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.SiegeEngines.Messages;
using GameInterface.Services.SiegeEnginesConstructionProgress.Messages;

namespace Coop.Core.Client.Services.SiegeEngines.Handlers;

/// <summary>
/// Forwards replicated siege engine container and construction progress changes to GameInterface, and
/// sends the local player's engine build/remove orders to the server.
/// </summary>
internal class ClientSiegeEngineHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    public ClientSiegeEngineHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        messageBroker.Subscribe<NetworkChangeSiegeEngineDeployed>(HandleDeployed);
        messageBroker.Subscribe<NetworkChangeSiegeEngineUndeployed>(HandleUndeployed);
        messageBroker.Subscribe<NetworkChangeSiegeEngineReserveAdded>(HandleReserveAdded);
        messageBroker.Subscribe<NetworkChangeSiegeEngineReserveRemoved>(HandleReserveRemoved);
        messageBroker.Subscribe<NetworkChangeSiegeEngineProgress>(HandleProgress);
        messageBroker.Subscribe<SiegeEngineDeployRequested>(HandleDeployRequested);
        messageBroker.Subscribe<SiegeEngineRemovalRequested>(HandleRemovalRequested);
    }

    private void HandleDeployed(MessagePayload<NetworkChangeSiegeEngineDeployed> payload)
    {
        var obj = payload.What;
        messageBroker.Publish(this, new ChangeSiegeEngineDeployed(obj.ContainerId, obj.SiegeEngineId, obj.EngineTypeId, obj.Index));
    }

    private void HandleUndeployed(MessagePayload<NetworkChangeSiegeEngineUndeployed> payload)
    {
        var obj = payload.What;
        messageBroker.Publish(this, new ChangeSiegeEngineUndeployed(obj.ContainerId, obj.Index, obj.IsRanged, obj.MoveToReserve));
    }

    private void HandleReserveAdded(MessagePayload<NetworkChangeSiegeEngineReserveAdded> payload)
    {
        var obj = payload.What;
        messageBroker.Publish(this, new ChangeSiegeEngineReserveAdded(obj.ContainerId, obj.SiegeEngineId, obj.EngineTypeId));
    }

    private void HandleReserveRemoved(MessagePayload<NetworkChangeSiegeEngineReserveRemoved> payload)
    {
        var obj = payload.What;
        messageBroker.Publish(this, new ChangeSiegeEngineReserveRemoved(obj.ContainerId, obj.SiegeEngineId));
    }

    private void HandleProgress(MessagePayload<NetworkChangeSiegeEngineProgress> payload)
    {
        var obj = payload.What;
        messageBroker.Publish(this, new ChangeSiegeEngineProgress(obj.SiegeEngineId, obj.IsRedeployment, obj.Value));
    }

    // Runs on the game thread already — published from the production-popup container patch; only resolves an id and sends, so no GameThread.RunSafe.
    private void HandleDeployRequested(MessagePayload<SiegeEngineDeployRequested> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.SiegeEvent, out var siegeEventId)) return;

        network.SendAll(new NetworkRequestDeploySiegeEngine(siegeEventId, (int)obj.Side, obj.EngineType.StringId, obj.Index));
    }

    private void HandleRemovalRequested(MessagePayload<SiegeEngineRemovalRequested> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.SiegeEvent, out var siegeEventId)) return;

        network.SendAll(new NetworkRequestRemoveSiegeEngine(siegeEventId, (int)obj.Side, obj.Index, obj.IsRanged, obj.MoveToReserve));
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkChangeSiegeEngineDeployed>(HandleDeployed);
        messageBroker.Unsubscribe<NetworkChangeSiegeEngineUndeployed>(HandleUndeployed);
        messageBroker.Unsubscribe<NetworkChangeSiegeEngineReserveAdded>(HandleReserveAdded);
        messageBroker.Unsubscribe<NetworkChangeSiegeEngineReserveRemoved>(HandleReserveRemoved);
        messageBroker.Unsubscribe<NetworkChangeSiegeEngineProgress>(HandleProgress);
        messageBroker.Unsubscribe<SiegeEngineDeployRequested>(HandleDeployRequested);
        messageBroker.Unsubscribe<SiegeEngineRemovalRequested>(HandleRemovalRequested);
    }
}
