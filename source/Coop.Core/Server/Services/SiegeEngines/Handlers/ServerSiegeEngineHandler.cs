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
        messageBroker.Subscribe<SiegeEngineHitpointsChanged>(HandleHitpoints);
        messageBroker.Subscribe<SiegeEngineMissileAdded>(HandleMissileAdded);
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

    // Runs on the game thread already — published from the container-mutation patch; only resolves ids and broadcasts, so no GameThread.RunSafe.
    private void HandleDeployed(MessagePayload<SiegeEngineDeployed> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.Container, out var containerId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.SiegeEngine, out var siegeEngineId)) return;

        network.SendAll(new NetworkChangeSiegeEngineDeployed(containerId, siegeEngineId, obj.SiegeEngine.SiegeEngine?.StringId, obj.Index));
    }

    // Runs on the game thread already — published from the container-mutation patch; only resolves an id and broadcasts, so no GameThread.RunSafe.
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

    // Runs on the game thread already — published from the construction-progress patch; only resolves an id and broadcasts, so no GameThread.RunSafe.
    private void HandleProgress(MessagePayload<SiegeEngineProgressChanged> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.SiegeEngine, out var siegeEngineId)) return;

        network.SendAll(new NetworkChangeSiegeEngineProgress(siegeEngineId, obj.IsRedeployment, obj.Value));
    }

    // Runs on the game thread already — published from the hitpoints patch / late registration; resolves an id and broadcasts.
    private void HandleHitpoints(MessagePayload<SiegeEngineHitpointsChanged> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.SiegeEngine, out var siegeEngineId)) return;

        network.SendAll(new NetworkChangeSiegeEngineHitpoints(siegeEngineId, obj.Hitpoints, obj.MaxHitPoints));
    }

    // Runs on the game thread already — published from the bombardment patch; resolves ids and broadcasts the visual missile.
    private void HandleMissileAdded(MessagePayload<SiegeEngineMissileAdded> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.SiegeEvent, out var siegeEventId)) return;

        string targetEngineId = null;
        if (obj.TargetSiegeEngine != null) objectManager.TryGetId(obj.TargetSiegeEngine, out targetEngineId);

        network.SendAll(new NetworkAddSiegeEngineMissile(siegeEventId, (int)obj.Side, obj.ShooterType?.StringId,
            obj.ShooterSlotIndex, (int)obj.TargetType, obj.TargetSlotIndex, targetEngineId,
            obj.CollisionTicks, obj.FireTicks, obj.HitSuccessful));
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SiegeEngineDeployed>(HandleDeployed);
        messageBroker.Unsubscribe<SiegeEngineUndeployed>(HandleUndeployed);
        messageBroker.Unsubscribe<SiegeEngineReserveAdded>(HandleReserveAdded);
        messageBroker.Unsubscribe<SiegeEngineReserveRemoved>(HandleReserveRemoved);
        messageBroker.Unsubscribe<SiegeEngineProgressChanged>(HandleProgress);
        messageBroker.Unsubscribe<SiegeEngineHitpointsChanged>(HandleHitpoints);
        messageBroker.Unsubscribe<SiegeEngineMissileAdded>(HandleMissileAdded);
        messageBroker.Unsubscribe<NetworkRequestDeploySiegeEngine>(HandleDeployRequest);
        messageBroker.Unsubscribe<NetworkRequestRemoveSiegeEngine>(HandleRemoveRequest);
    }
}
