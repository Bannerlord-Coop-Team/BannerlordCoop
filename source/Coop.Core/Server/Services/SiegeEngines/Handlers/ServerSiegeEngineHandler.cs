using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.SiegeEngines.Messages;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.SiegeEngines.Messages;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.SiegeEngines.Messages;
using GameInterface.Services.SiegeEvents.Interfaces;
using GameInterface.Services.SiegeEnginesConstructionProgress.Messages;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Linq;
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
    private readonly object revisionGate = new object();
    private readonly ConcurrentDictionary<string, SiegeEngineSlotRevision> slotRevisions = new ConcurrentDictionary<string, SiegeEngineSlotRevision>();
    private string revisionEpoch = NewRevisionEpoch();

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
        messageBroker.Subscribe<PlayerCampaignEntered>(HandlePlayerCampaignEntered);
        messageBroker.Subscribe<CampaignReady>(HandleCampaignReady);
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

            var side = (BattleSideEnum)obj.Side;
            if (!TryGetContainer(siegeEvent, side, out var container)
                || !objectManager.TryGetIdWithLogging(container, out var containerId))
            {
                Logger.Warning("Rejecting siege engine deploy for {SiegeEventId} side {Side} slot {Index}; slot context is unavailable",
                    obj.SiegeEventId, side, obj.Index);
                return;
            }

            GetSlotState(containerId, engineType.IsRanged, obj.Index, out var currentRevision, out var currentRevisionEpoch);
            if (!SlotMatchesExpectedState(
                    container,
                    obj.Index,
                    engineType.IsRanged,
                    obj.ExpectedOccupantId,
                    obj.ExpectedRevision,
                    obj.RevisionEpoch,
                    currentRevision,
                    currentRevisionEpoch,
                    objectManager))
            {
                Logger.Warning("Rejecting stale siege engine deploy for {SiegeEventId} side {Side} slot {Index}; expected occupant {ExpectedOccupantId} at revision {ExpectedRevision}",
                    obj.SiegeEventId, side, obj.Index, obj.ExpectedOccupantId, obj.ExpectedRevision);
                return;
            }

            siegeEventInterface.DeploySiegeEngine(siegeEvent, side, engineType, obj.Index);
        });
    }

    private void HandleRemoveRequest(MessagePayload<NetworkRequestRemoveSiegeEngine> payload)
    {
        var obj = payload.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<SiegeEvent>(obj.SiegeEventId, out var siegeEvent)) return;

            var side = (BattleSideEnum)obj.Side;
            if (!TryGetContainer(siegeEvent, side, out var container)
                || !objectManager.TryGetIdWithLogging(container, out var containerId))
            {
                Logger.Warning("Rejecting siege engine removal for {SiegeEventId} side {Side} slot {Index}; slot context is unavailable",
                    obj.SiegeEventId, side, obj.Index);
                return;
            }

            GetSlotState(containerId, obj.IsRanged, obj.Index, out var currentRevision, out var currentRevisionEpoch);
            if (!SlotMatchesExpectedState(
                    container,
                    obj.Index,
                    obj.IsRanged,
                    obj.ExpectedOccupantId,
                    obj.ExpectedRevision,
                    obj.RevisionEpoch,
                    currentRevision,
                    currentRevisionEpoch,
                    objectManager))
            {
                Logger.Warning("Rejecting stale siege engine removal for {SiegeEventId} side {Side} slot {Index}; expected occupant {ExpectedOccupantId} at revision {ExpectedRevision}",
                    obj.SiegeEventId, side, obj.Index, obj.ExpectedOccupantId, obj.ExpectedRevision);
                return;
            }

            siegeEventInterface.RemoveDeployedSiegeEngine(siegeEvent, side, obj.Index, obj.IsRanged, obj.MoveToReserve);
        });
    }

    private static bool TryGetContainer(SiegeEvent siegeEvent, BattleSideEnum side, out SiegeEnginesContainer container)
    {
        container = null;
        if (side != BattleSideEnum.Attacker && side != BattleSideEnum.Defender) return false;

        container = siegeEvent.GetSiegeEventSide(side)?.SiegeEngines;
        return container != null;
    }

    /// <summary>
    /// Optimistic slot-generation check. The revision rejects ABA (including the same engine returning to the
    /// slot), while the occupant id verifies that client and server agree on the contents of that generation.
    /// </summary>
    internal static bool SlotMatchesExpectedState(
        SiegeEnginesContainer container,
        int index,
        bool isRanged,
        string expectedOccupantId,
        long expectedRevision,
        string expectedRevisionEpoch,
        long currentRevision,
        string currentRevisionEpoch,
        IObjectManager objectManager)
    {
        if (container == null || objectManager == null) return false;
        if (!string.Equals(expectedRevisionEpoch, currentRevisionEpoch, StringComparison.Ordinal)) return false;
        if (expectedRevision != currentRevision) return false;

        var slots = isRanged ? container.DeployedRangedSiegeEngines : container.DeployedMeleeSiegeEngines;
        if (index < 0 || index >= slots.Length) return false;

        var currentOccupant = slots[index];
        if (currentOccupant == null) return expectedOccupantId == null;
        if (string.IsNullOrEmpty(expectedOccupantId)) return false;

        return objectManager.TryGetId(currentOccupant, out var currentOccupantId)
            && string.Equals(currentOccupantId, expectedOccupantId, StringComparison.Ordinal);
    }

    private static string SlotKey(string containerId, bool isRanged, int index)
    {
        return containerId + "|" + (isRanged ? "r" : "m") + "|" + index;
    }

    private static string NewRevisionEpoch() => Guid.NewGuid().ToString("N");

    private void GetSlotState(string containerId, bool isRanged, int index, out long revision, out string epoch)
    {
        lock (revisionGate)
        {
            epoch = revisionEpoch;
            revision = slotRevisions.TryGetValue(SlotKey(containerId, isRanged, index), out var slot)
                ? slot.Revision
                : 0L;
        }
    }

    // Caller holds revisionGate through both generation advance and network send so a snapshot cannot publish
    // the new generation before the delta that creates it.
    private long AdvanceSlotRevisionLocked(string containerId, bool isRanged, int index)
    {
        var updated = slotRevisions.AddOrUpdate(
            SlotKey(containerId, isRanged, index),
            _ => new SiegeEngineSlotRevision(containerId, isRanged, index, 1L),
            (_, current) => new SiegeEngineSlotRevision(containerId, isRanged, index, checked(current.Revision + 1L)));
        return updated.Revision;
    }

    private void HandlePlayerCampaignEntered(MessagePayload<PlayerCampaignEntered> payload)
    {
        lock (revisionGate)
        {
            var snapshot = new NetworkSyncSiegeEngineSlotRevisions(revisionEpoch, slotRevisions.Values.ToArray());
            network.Send(payload.What.playerId, snapshot);
        }
    }

    private void HandleCampaignReady(MessagePayload<CampaignReady> payload)
    {
        lock (revisionGate)
        {
            slotRevisions.Clear();
            revisionEpoch = NewRevisionEpoch();
        }
    }

    // Runs on the game thread already — published from the container-mutation patch; only resolves ids and broadcasts, so no GameThread.RunSafe.
    private void HandleDeployed(MessagePayload<SiegeEngineDeployed> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.Container, out var containerId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.SiegeEngine, out var siegeEngineId)) return;

        bool isRanged = obj.SiegeEngine.SiegeEngine?.IsRanged == true;
        lock (revisionGate)
        {
            long slotRevision = AdvanceSlotRevisionLocked(containerId, isRanged, obj.Index);
            network.SendAll(new NetworkChangeSiegeEngineDeployed(
                containerId,
                siegeEngineId,
                obj.SiegeEngine.SiegeEngine?.StringId,
                obj.Index,
                slotRevision,
                isRanged,
                revisionEpoch));
        }
    }

    // Runs on the game thread already — published from the container-mutation patch; only resolves an id and broadcasts, so no GameThread.RunSafe.
    private void HandleUndeployed(MessagePayload<SiegeEngineUndeployed> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.Container, out var containerId)) return;

        lock (revisionGate)
        {
            long slotRevision = AdvanceSlotRevisionLocked(containerId, obj.IsRanged, obj.Index);
            network.SendAll(new NetworkChangeSiegeEngineUndeployed(
                containerId,
                obj.Index,
                obj.IsRanged,
                obj.MoveToReserve,
                slotRevision,
                revisionEpoch));
        }
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
        messageBroker.Unsubscribe<PlayerCampaignEntered>(HandlePlayerCampaignEntered);
        messageBroker.Unsubscribe<CampaignReady>(HandleCampaignReady);
    }
}
