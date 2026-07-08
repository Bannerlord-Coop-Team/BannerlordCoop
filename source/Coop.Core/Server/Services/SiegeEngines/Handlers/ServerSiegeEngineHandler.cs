using System.Linq;
using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.SiegeEngines.Messages;
using Coop.Core.Server.Services.SiegeEngines.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.SiegeEngines.Messages;
using GameInterface.Services.SiegeEvents.Interfaces;
using GameInterface.Services.SiegeEnginesConstructionProgress.Messages;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace Coop.Core.Server.Services.SiegeEngines.Handlers;

/// <summary>
/// Broadcasts server-side siege engine container and construction progress changes to clients, and
/// applies client engine build/remove orders authoritatively.
/// </summary>
internal class ServerSiegeEngineHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ServerSiegeEngineHandler>();

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

        if (!objectManager.TryGetId(obj.SiegeEngine, out var siegeEngineId))
        {
            LogUnregisteredProgress(obj);
            return;
        }

        network.SendAll(new NetworkChangeSiegeEngineProgress(siegeEngineId, obj.IsRedeployment, obj.Value));
    }

    // TEMP diagnostic: some ticking construction progress isn't in the co-op registry, so its progress can't
    // broadcast. Name the engine type, role, side and settlement so we can register the creation path that misses it.
    private static void LogUnregisteredProgress(SiegeEngineProgressChanged obj)
    {
        var progress = obj.SiegeEngine;
        string role = "none", side = "none", settlement = "none";

        foreach (var siegeEvent in Campaign.Current?.SiegeEventManager?.SiegeEvents ?? Enumerable.Empty<SiegeEvent>())
        {
            if (siegeEvent?.BesiegedSettlement == null) continue;

            if (Locate(siegeEvent.BesiegerCamp?.SiegeEngines, progress, out role)) { side = "attacker"; settlement = siegeEvent.BesiegedSettlement.StringId; break; }
            if (Locate(siegeEvent.BesiegedSettlement.SiegeEngines, progress, out role)) { side = "defender"; settlement = siegeEvent.BesiegedSettlement.StringId; break; }
        }

        Logger.Error("[ProgressDiag] unregistered progress engine={Engine} redeploy={Redeploy} value={Value:0.00} constructed={Constructed} role={Role} side={Side} settlement={Settlement}",
            progress.SiegeEngine?.StringId ?? "null", obj.IsRedeployment, obj.Value, progress.IsConstructed, role, side, settlement);
    }

    private static bool Locate(SiegeEnginesContainer container, SiegeEngineConstructionProgress progress, out string role)
    {
        role = "none";
        if (container == null) return false;

        if (container.SiegePreparations == progress) { role = "prep"; return true; }
        for (int i = 0; i < container.DeployedSiegeEngines.Count; i++)
            if (container.DeployedSiegeEngines[i] == progress) { role = "deployed[" + i + "]"; return true; }
        for (int i = 0; i < container.ReservedSiegeEngines.Count; i++)
            if (container.ReservedSiegeEngines[i] == progress) { role = "reserved[" + i + "]"; return true; }

        return false;
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
