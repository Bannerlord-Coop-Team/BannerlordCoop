using Common;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.SiegeEngines.Messages;
using Coop.Core.Server.Services.SiegeEngines.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.SiegeEngines.Messages;
using GameInterface.Services.SiegeEvents.Interfaces;
using GameInterface.Services.SiegeEnginesConstructionProgress.Messages;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace Coop.Core.Server.Services.SiegeEngines.Handlers;

/// <summary>
/// Broadcasts server-side siege engine container and construction progress changes to clients, and
/// applies client engine build/remove orders authoritatively.
/// </summary>
internal class ServerSiegeEngineHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly ISiegeEventInterface siegeEventInterface;

    public ServerSiegeEngineHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager, ISiegeEventInterface siegeEventInterface)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.siegeEventInterface = siegeEventInterface;
        messageBroker.Subscribe<SiegeEngineDeployed>(HandleDeployed);
        messageBroker.Subscribe<SiegeEngineUndeployed>(HandleUndeployed);
        messageBroker.Subscribe<SiegeEngineReserveAdded>(HandleReserveAdded);
        messageBroker.Subscribe<SiegeEngineReserveRemoved>(HandleReserveRemoved);
        messageBroker.Subscribe<SiegeEngineProgressChanged>(HandleProgress);
        messageBroker.Subscribe<NetworkRequestDeploySiegeEngine>(HandleDeployRequest);
        messageBroker.Subscribe<NetworkRequestRemoveSiegeEngine>(HandleRemoveRequest);
    }

    private void HandleDeployRequest(MessagePayload<NetworkRequestDeploySiegeEngine> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<SiegeEvent>(obj.SiegeEventId, out var siegeEvent)) return;

            // Catalog SiegeEngineTypes are XML objects the co-op registry never holds; resolve
            // through the game's own object manager.
            var engineType = MBObjectManager.Instance.GetObject<SiegeEngineType>(obj.EngineTypeId);
            if (engineType == null) return;

            siegeEventInterface.DeploySiegeEngine(siegeEvent, (BattleSideEnum)obj.Side, engineType, obj.Index);
        });
    }

    private void HandleRemoveRequest(MessagePayload<NetworkRequestRemoveSiegeEngine> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<SiegeEvent>(obj.SiegeEventId, out var siegeEvent)) return;

            siegeEventInterface.RemoveDeployedSiegeEngine(siegeEvent, (BattleSideEnum)obj.Side, obj.Index, obj.IsRanged, obj.MoveToReserve);
        });
    }

    private void HandleDeployed(MessagePayload<SiegeEngineDeployed> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.Container, out var containerId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.SiegeEngine, out var siegeEngineId)) return;

        network.SendAll(new NetworkChangeSiegeEngineDeployed(containerId, siegeEngineId, obj.SiegeEngine.SiegeEngine?.StringId, obj.Index));
    }

    private void HandleUndeployed(MessagePayload<SiegeEngineUndeployed> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.Container, out var containerId)) return;

        network.SendAll(new NetworkChangeSiegeEngineUndeployed(containerId, obj.Index, obj.IsRanged, obj.MoveToReserve));
    }

    private void HandleReserveAdded(MessagePayload<SiegeEngineReserveAdded> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.Container, out var containerId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.SiegeEngine, out var siegeEngineId)) return;

        network.SendAll(new NetworkChangeSiegeEngineReserveAdded(containerId, siegeEngineId, obj.SiegeEngine.SiegeEngine?.StringId));
    }

    private void HandleReserveRemoved(MessagePayload<SiegeEngineReserveRemoved> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.Container, out var containerId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.SiegeEngine, out var siegeEngineId)) return;

        network.SendAll(new NetworkChangeSiegeEngineReserveRemoved(containerId, siegeEngineId));
    }

    private void HandleProgress(MessagePayload<SiegeEngineProgressChanged> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.SiegeEngine, out var siegeEngineId)) return;

        network.SendAll(new NetworkChangeSiegeEngineProgress(siegeEngineId, obj.IsRedeployment, obj.Value));
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SiegeEngineDeployed>(HandleDeployed);
        messageBroker.Unsubscribe<SiegeEngineUndeployed>(HandleUndeployed);
        messageBroker.Unsubscribe<SiegeEngineReserveAdded>(HandleReserveAdded);
        messageBroker.Unsubscribe<SiegeEngineReserveRemoved>(HandleReserveRemoved);
        messageBroker.Unsubscribe<SiegeEngineProgressChanged>(HandleProgress);
        messageBroker.Unsubscribe<NetworkRequestDeploySiegeEngine>(HandleDeployRequest);
        messageBroker.Unsubscribe<NetworkRequestRemoveSiegeEngine>(HandleRemoveRequest);
    }
}
