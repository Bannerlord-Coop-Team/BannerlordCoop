using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.ObjectManager;
using Missions.Agents.Messages;
using Missions.Agents.Packets;
using Missions.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Handlers
{
    /// <summary>
    /// Handler for weapon pickups within a battle
    /// </summary>
    public interface IWeaponPickupHandler : IHandler
    {

    }
    /// <inheritdoc/>
    public class WeaponPickupHandler : IWeaponPickupHandler
    {
        /// <summary>Retains the authoritative picker id while a runtime item waits for its canonical id.</summary>
        private readonly struct PendingIdentityPickup
        {
            public Guid AgentId { get; }
            public WeaponPickedup Pickup { get; }

            public PendingIdentityPickup(Guid agentId, WeaponPickedup pickup)
            {
                AgentId = agentId;
                Pickup = pickup;
            }
        }

        readonly INetworkAgentRegistry networkAgentRegistry;
        readonly INetworkWorldItemRegistry worldItemRegistry;
        readonly IBattleNetwork network;
        readonly IMessageBroker messageBroker;
        readonly IObjectManager objectManager;
        readonly Dictionary<SpawnedItemEntity, Queue<PendingIdentityPickup>> pendingIdentityPickups =
            new Dictionary<SpawnedItemEntity, Queue<PendingIdentityPickup>>();
        readonly Dictionary<SpawnedItemEntity, Queue<PendingIdentityPickup>> abandonedIdentityPickups =
            new Dictionary<SpawnedItemEntity, Queue<PendingIdentityPickup>>();
        readonly HashSet<SpawnedItemEntity> pendingWorldItemIdentities =
            new HashSet<SpawnedItemEntity>();
        readonly static ILogger Logger = LogManager.GetLogger<WeaponPickupHandler>();
#if DEBUG
        private const short PartialFixtureSourceAmount = 30;
        private const short PartialFixtureDroppedAmount = 8;
        private const short PartialFixtureMaximumAmount = 32;

        private sealed class PartialConsumableFixtureSnapshot
        {
            public Agent Agent { get; set; }
            public Guid AgentId { get; set; }
            public EquipmentIndex SourceSlot { get; set; }
            public EquipmentIndex DropSlot { get; set; }
            public MissionWeapon SourceWeapon { get; set; }
            public MissionWeapon DropWeapon { get; set; }
            public AgentEquipmentData Equipment { get; set; }
            public bool LocallyControlled { get; set; }
            public string ControllerId { get; set; }
            public Guid WorldItemId { get; set; }
        }

        private readonly Dictionary<Guid, PartialConsumableFixtureSnapshot> partialConsumableFixtures =
            new Dictionary<Guid, PartialConsumableFixtureSnapshot>();
#endif
        public WeaponPickupHandler(
            INetworkAgentRegistry networkAgentRegistry,
            INetworkWorldItemRegistry worldItemRegistry,
            IBattleNetwork network,
            IMessageBroker messageBroker,
            IObjectManager objectManager)
        {
            this.networkAgentRegistry = networkAgentRegistry;
            this.worldItemRegistry = worldItemRegistry;
            this.network = network;
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;

            messageBroker.Subscribe<WeaponPickedup>(WeaponPickupSend);
            messageBroker.Subscribe<NetworkWeaponPickedup>(WeaponPickupReceive);
            messageBroker.Subscribe<WorldItemIdentityResolved>(HandleWorldItemIdentityResolved);
            messageBroker.Subscribe<WorldItemIdentityPending>(HandleWorldItemIdentityPending);
            messageBroker.Subscribe<WorldItemIdentityAbandoned>(HandleWorldItemIdentityAbandoned);
#if DEBUG
            messageBroker.Subscribe<PreparePartialConsumablePickupFixture>(PreparePartialConsumableFixtureSend);
            messageBroker.Subscribe<NetworkPreparePartialConsumablePickupFixture>(PreparePartialConsumableFixtureReceive);
            messageBroker.Subscribe<RestorePartialConsumablePickupFixture>(RestorePartialConsumableFixtureSend);
            messageBroker.Subscribe<NetworkRestorePartialConsumablePickupFixture>(RestorePartialConsumableFixtureReceive);
            messageBroker.Subscribe<WeaponDropped>(TrackPartialConsumableFixtureWorldItemSend);
            messageBroker.Subscribe<NetworkWeaponDropped>(TrackPartialConsumableFixtureWorldItemReceive);
            messageBroker.Subscribe<MissionPeerLeft>(HandleFixtureOwnerLeft);
            messageBroker.Subscribe<MissionPeerDisconnected>(HandleFixtureOwnerDisconnected);
#endif

        }
        ~WeaponPickupHandler()
        {
            Dispose();
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<WeaponPickedup>(WeaponPickupSend);
            messageBroker.Unsubscribe<NetworkWeaponPickedup>(WeaponPickupReceive);
            messageBroker.Unsubscribe<WorldItemIdentityResolved>(HandleWorldItemIdentityResolved);
            messageBroker.Unsubscribe<WorldItemIdentityPending>(HandleWorldItemIdentityPending);
            messageBroker.Unsubscribe<WorldItemIdentityAbandoned>(HandleWorldItemIdentityAbandoned);
            pendingIdentityPickups.Clear();
            abandonedIdentityPickups.Clear();
            pendingWorldItemIdentities.Clear();
#if DEBUG
            messageBroker.Unsubscribe<PreparePartialConsumablePickupFixture>(PreparePartialConsumableFixtureSend);
            messageBroker.Unsubscribe<NetworkPreparePartialConsumablePickupFixture>(PreparePartialConsumableFixtureReceive);
            messageBroker.Unsubscribe<RestorePartialConsumablePickupFixture>(RestorePartialConsumableFixtureSend);
            messageBroker.Unsubscribe<NetworkRestorePartialConsumablePickupFixture>(RestorePartialConsumableFixtureReceive);
            messageBroker.Unsubscribe<WeaponDropped>(TrackPartialConsumableFixtureWorldItemSend);
            messageBroker.Unsubscribe<NetworkWeaponDropped>(TrackPartialConsumableFixtureWorldItemReceive);
            messageBroker.Unsubscribe<MissionPeerLeft>(HandleFixtureOwnerLeft);
            messageBroker.Unsubscribe<MissionPeerDisconnected>(HandleFixtureOwnerDisconnected);
            partialConsumableFixtures.Clear();
#endif
        }

        private void WeaponPickupSend(MessagePayload<WeaponPickedup> obj)
        {
            var payload = obj.What;

            if (!networkAgentRegistry.IsLocallyControlled(payload.Agent))
                return;

            if (!networkAgentRegistry.TryGetAgentInfo(payload.Agent, out var agentInfo))
            {
                Logger.Warning("No agentID was found for the Agent: {agent}", payload.Agent);
                return;
            }

            if (payload.WorldItem == null)
            {
                Logger.Error("No world item was found for a weapon pickup");
                return;
            }

            Guid worldItemId;
            if (payload.WorldItem.Id.CreatedAtRuntime &&
                !worldItemRegistry.TryGetId(payload.WorldItem, out worldItemId))
            {
                if (!pendingWorldItemIdentities.Contains(payload.WorldItem))
                {
                    SendWeaponPickup(payload, agentInfo.AgentId, Guid.Empty);
                    return;
                }

                if (!pendingIdentityPickups.TryGetValue(
                        payload.WorldItem,
                        out Queue<PendingIdentityPickup> pending))
                {
                    pending = new Queue<PendingIdentityPickup>();
                    pendingIdentityPickups.Add(payload.WorldItem, pending);
                }
                pending.Enqueue(new PendingIdentityPickup(agentInfo.AgentId, payload));
                Logger.Debug("Deferred weapon pickup until the runtime world item receives its canonical identity");
                return;
            }
            if (!payload.WorldItem.Id.CreatedAtRuntime)
                worldItemId = worldItemRegistry.GetOrCreateId(payload.WorldItem);

            SendWeaponPickup(payload, agentInfo.AgentId, worldItemId);
        }

        private void SendWeaponPickup(
            WeaponPickedup payload,
            Guid agentId,
            Guid worldItemId,
            bool isIdentityCorrection = false)
        {
            if (!objectManager.TryGetIdWithLogging(payload.WeaponObject, out string itemObjectId))
                return;
            if (payload.ResultingSlotWeapon.IsEmpty ||
                !objectManager.TryGetIdWithLogging(
                    payload.ResultingSlotWeapon.Item,
                    out string resultingSlotItemObjectId))
            {
                Logger.Error("No resulting slot weapon was found for a weapon pickup");
                return;
            }
            string resultingSlotItemModifierId = null;
            if (payload.ResultingSlotWeapon.ItemModifier != null &&
                !objectManager.TryGetIdWithLogging(
                    payload.ResultingSlotWeapon.ItemModifier,
                    out resultingSlotItemModifierId))
            {
                return;
            }

            NetworkWeaponPickedup message = new NetworkWeaponPickedup(
                agentId,
                payload.EquipmentIndex,
                worldItemId,
                itemObjectId,
                payload.WeaponModifier,
                payload.Banner,
                payload.CurrentEquipment,
                payload.PreviousSlotAmount,
                payload.PreviousWorldItemAmount,
                payload.ResultingSlotAmount,
                payload.ResultingWorldItemAmount,
                payload.WorldItemConsumed,
                resultingSlotItemObjectId,
                resultingSlotItemModifierId,
                payload.ResultingSlotWeapon.Banner,
                payload.ResultingSlotWeapon.RawDataForNetwork,
                isIdentityCorrection);

            network.SendAll(message);
        }

        private void HandleWorldItemIdentityResolved(MessagePayload<WorldItemIdentityResolved> payload)
        {
            WorldItemIdentityResolved resolved = payload.What;
            if (resolved.WorldItem == null || resolved.WorldItemId == Guid.Empty)
            {
                return;
            }

            pendingWorldItemIdentities.Remove(resolved.WorldItem);
            bool isIdentityCorrection = false;
            if (!pendingIdentityPickups.TryGetValue(
                    resolved.WorldItem,
                    out Queue<PendingIdentityPickup> pending))
            {
                if (!abandonedIdentityPickups.TryGetValue(resolved.WorldItem, out pending))
                    return;
                isIdentityCorrection = true;
                abandonedIdentityPickups.Remove(resolved.WorldItem);
            }

            pendingIdentityPickups.Remove(resolved.WorldItem);
            while (pending.Count > 0)
            {
                PendingIdentityPickup pendingPickup = pending.Dequeue();
                SendWeaponPickup(
                    pendingPickup.Pickup,
                    pendingPickup.AgentId,
                    resolved.WorldItemId,
                    isIdentityCorrection);
            }
        }

        private void HandleWorldItemIdentityPending(MessagePayload<WorldItemIdentityPending> payload)
        {
            if (payload.What.WorldItem != null)
                pendingWorldItemIdentities.Add(payload.What.WorldItem);
        }

        private void HandleWorldItemIdentityAbandoned(MessagePayload<WorldItemIdentityAbandoned> payload)
        {
            SpawnedItemEntity worldItem = payload.What.WorldItem;
            if (worldItem == null) return;

            pendingWorldItemIdentities.Remove(worldItem);
            if (!payload.What.AwaitLateResolution)
                abandonedIdentityPickups.Remove(worldItem);
            if (!pendingIdentityPickups.TryGetValue(
                    worldItem,
                    out Queue<PendingIdentityPickup> pending))
            {
                return;
            }

            pendingIdentityPickups.Remove(worldItem);
            if (payload.What.AwaitLateResolution)
                abandonedIdentityPickups[worldItem] = pending;
            foreach (PendingIdentityPickup pendingPickup in pending)
            {
                SendWeaponPickup(
                    pendingPickup.Pickup,
                    pendingPickup.AgentId,
                    Guid.Empty);
            }
        }
        private void WeaponPickupReceive(MessagePayload<NetworkWeaponPickedup> obj)
        {
            GameThread.RunSafe(() =>
            {
                if (obj.What.IsIdentityCorrection)
                {
                    messageBroker.Publish(
                        this,
                        new WeaponPickupApplied(
                            obj.What.AgentId,
                            obj.What.EquipmentIndex,
                            obj.What.WorldItemId,
                            obj.What.ResultingWorldItemAmount,
                            obj.What.WorldItemConsumed));
                    return;
                }

                if (!objectManager.TryGetObjectWithLogging<ItemObject>(obj.What.ItemObjectId, out var itemObject))
                    return;

                SpawnedItemEntity worldItem = null;
                if (obj.What.WorldItemId != Guid.Empty)
                {
                    if (!TryGetWorldItem(obj.What.WorldItemId, out worldItem))
                    {
                        Logger.Warning(
                            "Applying weapon pickup before world item {WorldItemId} was registered",
                            obj.What.WorldItemId);
                    }
                    else if (!ReferenceEquals(worldItem.WeaponCopy.Item, itemObject))
                    {
                        Logger.Error(
                            "World item {WorldItemId} does not contain item {ItemObjectId}",
                            obj.What.WorldItemId,
                            obj.What.ItemObjectId);
                        return;
                    }
                }

                bool hasActiveAgent =
                    networkAgentRegistry.TryGetAgentInfo(obj.What.AgentId, out CoopAgentInfo agentInfo) &&
                    agentInfo.Agent != null &&
                    agentInfo.Agent.Mission == Mission.Current &&
                    agentInfo.Agent.IsActive();
                if (!hasActiveAgent)
                {
                    Logger.Warning(
                        "Retiring weapon pickup without active agent={AgentId}",
                        obj.What.AgentId);
                    messageBroker.Publish(
                        this,
                        new WeaponPickupApplied(
                            obj.What.AgentId,
                            obj.What.EquipmentIndex,
                            obj.What.WorldItemId,
                            obj.What.ResultingWorldItemAmount,
                            obj.What.WorldItemConsumed));
                    return;
                }

                MissionWeapon missionWeapon = new MissionWeapon(
                    itemObject,
                    obj.What.ItemModifier,
                    obj.What.Banner);
                MissionWeapon resultingSlotWeapon = missionWeapon;
                if (!string.IsNullOrEmpty(obj.What.ResultingSlotItemObjectId))
                {
                    if (!objectManager.TryGetObjectWithLogging<ItemObject>(
                            obj.What.ResultingSlotItemObjectId,
                            out ItemObject resultingSlotItem))
                    {
                        return;
                    }
                    ItemModifier resultingSlotModifier = null;
                    if (!string.IsNullOrEmpty(obj.What.ResultingSlotItemModifierId) &&
                        !objectManager.TryGetObjectWithLogging<ItemModifier>(
                            obj.What.ResultingSlotItemModifierId,
                            out resultingSlotModifier))
                    {
                        return;
                    }
                    resultingSlotWeapon = new MissionWeapon(
                        resultingSlotItem,
                        resultingSlotModifier,
                        obj.What.ResultingSlotBanner,
                        obj.What.ResultingSlotDataValue);
                }
                else
                {
                    resultingSlotWeapon.Amount = obj.What.ResultingSlotAmount;
                }
                ApplyWeaponPickup(
                    agentInfo,
                    worldItem,
                    obj.What.EquipmentIndex,
                    ref missionWeapon,
                    obj.What.CurrentEquipment,
                    obj.What.PreviousSlotAmount,
                    obj.What.PreviousWorldItemAmount,
                    obj.What.ResultingSlotAmount,
                    obj.What.ResultingWorldItemAmount,
                    obj.What.WorldItemConsumed,
                    ref resultingSlotWeapon);
                messageBroker.Publish(
                    this,
                    new WeaponPickupApplied(
                        obj.What.AgentId,
                        obj.What.EquipmentIndex,
                        obj.What.WorldItemId,
                        obj.What.ResultingWorldItemAmount,
                        obj.What.WorldItemConsumed));
            });
        }

        private bool TryGetWorldItem(Guid worldItemId, out SpawnedItemEntity worldItem)
        {
            if (worldItemRegistry.TryGet(worldItemId, out worldItem))
            {
                if (IsWorldItemAvailable(worldItem))
                    return true;

                worldItemRegistry.Remove(worldItemId);
                worldItem = null;
            }

            Mission mission = Mission.Current;
            if (mission == null) return false;

            foreach (MissionObject missionObject in mission.MissionObjects)
            {
                if (!(missionObject is SpawnedItemEntity candidate) ||
                    candidate.Id.CreatedAtRuntime ||
                    !IsWorldItemAvailable(candidate))
                    continue;
                if (worldItemRegistry.GetOrCreateId(candidate) != worldItemId)
                    continue;

                worldItem = candidate;
                return true;
            }

            return false;
        }

        internal static bool IsWorldItemAvailable(SpawnedItemEntity worldItem)
        {
            return worldItem != null &&
                IsWorldItemStateAvailable(
                    worldItem.IsRemoved,
                    worldItem.IsDeactivated,
                    worldItem.GameEntity.IsValid);
        }

        internal static bool IsWorldItemStateAvailable(
            bool isRemoved,
            bool isDeactivated,
            bool isGameEntityValid)
        {
            return !isRemoved && !isDeactivated && isGameEntityValid;
        }

        internal static void ApplyWeaponPickup(
            CoopAgentInfo agentInfo,
            SpawnedItemEntity worldItem,
            EquipmentIndex equipmentIndex,
            ref MissionWeapon missionWeapon,
            AgentEquipmentData currentEquipment,
            short previousSlotAmount,
            short previousWorldItemAmount,
            short resultingSlotAmount,
            short resultingWorldItemAmount,
            bool worldItemConsumed,
            ref MissionWeapon resultingSlotWeapon)
        {
            Agent agent = agentInfo.Agent;
            agentInfo.RecordAuthoritativeEquipment(currentEquipment);
            using (new AllowedThread())
            {
                ApplyWeaponAmounts(
                    agent,
                    worldItem,
                    equipmentIndex,
                    previousSlotAmount,
                    previousWorldItemAmount);

                bool slotContainsCanonical =
                    equipmentIndex >= EquipmentIndex.WeaponItemBeginSlot &&
                    equipmentIndex < EquipmentIndex.NumAllWeaponSlots &&
                    ReferenceEquals(agent.Equipment[equipmentIndex].Item, resultingSlotWeapon.Item);
                if (worldItem == null || (!worldItemConsumed && !slotContainsCanonical))
                    ApplyDetachedWeaponPickup(agent, equipmentIndex, ref resultingSlotWeapon);
                else if (worldItemConsumed)
                    ApplyWorldItemPickup(worldItem, agent, isSuccessful: true, (int)equipmentIndex);

                ApplyWeaponAmounts(
                    agent,
                    worldItem,
                    equipmentIndex,
                    resultingSlotAmount,
                    resultingWorldItemAmount);
                ReconcileResultingSlotWeapon(agent, equipmentIndex, ref resultingSlotWeapon);

                // Replay the owner's final hand selection after vanilla applies the world-item pickup.
                currentEquipment.Apply(agent);
            }
        }

        private static void ApplyWeaponAmounts(
            Agent agent,
            SpawnedItemEntity worldItem,
            EquipmentIndex equipmentIndex,
            short slotAmount,
            short worldItemAmount)
        {
            if (equipmentIndex >= EquipmentIndex.WeaponItemBeginSlot &&
                equipmentIndex < EquipmentIndex.NumAllWeaponSlots &&
                !agent.Equipment[equipmentIndex].IsEmpty)
            {
                agent.SetWeaponAmountInSlot(
                    equipmentIndex,
                    slotAmount,
                    enforcePrimaryItem: true);
            }

            if (worldItem != null)
                worldItem._weapon.Amount = worldItemAmount;
        }

        private static void ApplyWorldItemPickup(
            SpawnedItemEntity worldItem,
            Agent agent,
            bool isSuccessful,
            int preferenceIndex)
        {
            worldItem.OnUseStopped(agent, isSuccessful, preferenceIndex);
        }

        private static void ApplyDetachedWeaponPickup(
            Agent agent,
            EquipmentIndex equipmentIndex,
            ref MissionWeapon missionWeapon)
        {
            if (equipmentIndex == EquipmentIndex.ExtraWeaponSlot)
                agent.EquipWeaponToExtraSlotAndWield(ref missionWeapon);
            else
                agent.EquipWeaponWithNewEntity(equipmentIndex, ref missionWeapon);
        }

        private static void ReconcileResultingSlotWeapon(
            Agent agent,
            EquipmentIndex equipmentIndex,
            ref MissionWeapon resultingSlotWeapon)
        {
            if (equipmentIndex < EquipmentIndex.WeaponItemBeginSlot ||
                equipmentIndex >= EquipmentIndex.NumAllWeaponSlots)
                return;

            MissionWeapon current = agent.Equipment[equipmentIndex];
            if (WeaponMatches(current, resultingSlotWeapon)) return;

            if (!current.IsEmpty)
                agent.RemoveEquippedWeapon(equipmentIndex);
            if (!resultingSlotWeapon.IsEmpty)
                ApplyDetachedWeaponPickup(agent, equipmentIndex, ref resultingSlotWeapon);
        }

        private static bool WeaponMatches(MissionWeapon current, MissionWeapon canonical)
        {
            if (current.IsEmpty || canonical.IsEmpty)
                return current.IsEmpty && canonical.IsEmpty;

            return ReferenceEquals(current.Item, canonical.Item) &&
                ReferenceEquals(current.ItemModifier, canonical.ItemModifier) &&
                string.Equals(current.Banner?.Serialize(), canonical.Banner?.Serialize(), StringComparison.Ordinal) &&
                current.RawDataForNetwork == canonical.RawDataForNetwork;
        }

#if DEBUG
        private void PreparePartialConsumableFixtureSend(
            MessagePayload<PreparePartialConsumablePickupFixture> payload)
        {
            PreparePartialConsumablePickupFixture request = payload.What;
            request.Handled = true;

            if (!networkAgentRegistry.IsLocallyControlled(request.Agent) ||
                !networkAgentRegistry.TryGetAgentInfo(request.Agent, out CoopAgentInfo agentInfo))
            {
                request.Error = "agent-not-locally-controlled";
                return;
            }

            if (partialConsumableFixtures.ContainsKey(agentInfo.AgentId))
            {
                request.Error = "partial-fixture-active";
                return;
            }

            if (!TryFindPartialConsumableSlots(
                    request.Agent,
                    out EquipmentIndex sourceSlot,
                    out EquipmentIndex dropSlot,
                    out string itemObjectId,
                    out string error))
            {
                request.Error = error;
                return;
            }

            if (!TryApplyPartialConsumableFixture(
                    agentInfo,
                    sourceSlot,
                    dropSlot,
                    itemObjectId,
                    PartialFixtureSourceAmount,
                    PartialFixtureDroppedAmount,
                    locallyControlled: true,
                    out error))
            {
                request.Error = error;
                return;
            }

            request.SourceSlot = sourceSlot;
            request.DropSlot = dropSlot;
            request.ItemObjectId = itemObjectId;
            request.OriginalSourceAmount = partialConsumableFixtures[agentInfo.AgentId].SourceWeapon.Amount;
            request.SourceAmount = PartialFixtureSourceAmount;
            request.DroppedAmount = PartialFixtureDroppedAmount;
            request.Succeeded = true;

            network.SendAll(new NetworkPreparePartialConsumablePickupFixture(
                agentInfo.AgentId,
                sourceSlot,
                dropSlot,
                itemObjectId,
                PartialFixtureSourceAmount,
                PartialFixtureDroppedAmount));
        }

        private void PreparePartialConsumableFixtureReceive(
            MessagePayload<NetworkPreparePartialConsumablePickupFixture> payload)
        {
            GameThread.RunSafe(() =>
            {
                NetworkPreparePartialConsumablePickupFixture message = payload.What;
                if (networkAgentRegistry.IsLocallyControlled(message.AgentId))
                    return;

                if (!networkAgentRegistry.TryGetAgentInfo(message.AgentId, out CoopAgentInfo agentInfo))
                {
                    Logger.Warning("No agent found for partial consumable fixture {AgentId}", message.AgentId);
                    return;
                }

                using (new AllowedThread())
                {
                    if (!TryApplyPartialConsumableFixture(
                            agentInfo,
                            message.SourceSlot,
                            message.DropSlot,
                            message.ItemObjectId,
                            message.SourceAmount,
                            message.DroppedAmount,
                            locallyControlled: false,
                            out string error))
                    {
                        Logger.Error(
                            "Failed to apply partial consumable fixture for {AgentId}: {Error}",
                            message.AgentId,
                            error);
                    }
                }
            });
        }

        private void RestorePartialConsumableFixtureSend(
            MessagePayload<RestorePartialConsumablePickupFixture> payload)
        {
            RestorePartialConsumablePickupFixture request = payload.What;
            request.Handled = true;

            Guid agentId = request.AgentId;
            if (!partialConsumableFixtures.TryGetValue(
                    agentId,
                    out PartialConsumableFixtureSnapshot snapshot) ||
                !snapshot.LocallyControlled)
            {
                request.Error = "partial-fixture-not-locally-controlled";
                return;
            }

            if (!TryRestorePartialConsumableFixture(
                    agentId,
                    request.WorldItemId,
                    out string error,
                    out short sourceAmount,
                    out bool dropSlotEmpty,
                    out bool worldItemInactive))
            {
                request.Error = error;
                return;
            }

            request.RestoredSourceAmount = sourceAmount;
            request.DropSlotEmpty = dropSlotEmpty;
            request.WorldItemInactive = worldItemInactive;
            request.Succeeded = true;
            network.SendAll(new NetworkRestorePartialConsumablePickupFixture(
                agentId,
                request.WorldItemId));
        }

        private void RestorePartialConsumableFixtureReceive(
            MessagePayload<NetworkRestorePartialConsumablePickupFixture> payload)
        {
            GameThread.RunSafe(() =>
            {
                NetworkRestorePartialConsumablePickupFixture message = payload.What;
                if (partialConsumableFixtures.TryGetValue(
                        message.AgentId,
                        out PartialConsumableFixtureSnapshot snapshot) &&
                    snapshot.LocallyControlled)
                    return;

                using (new AllowedThread())
                {
                    if (!TryRestorePartialConsumableFixture(
                            message.AgentId,
                            message.WorldItemId,
                            out string error,
                            out _,
                            out _,
                            out _))
                    {
                        Logger.Error(
                            "Failed to restore partial consumable fixture for {AgentId}: {Error}",
                            message.AgentId,
                            error);
                    }
                }
            });
        }

        private bool TryFindPartialConsumableSlots(
            Agent agent,
            out EquipmentIndex sourceSlot,
            out EquipmentIndex dropSlot,
            out string itemObjectId,
            out string error)
        {
            sourceSlot = EquipmentIndex.None;
            dropSlot = EquipmentIndex.ExtraWeaponSlot;
            itemObjectId = null;
            error = null;

            if (!agent.Equipment[dropSlot].IsEmpty)
            {
                error = "extra-slot-not-empty";
                return false;
            }

            for (EquipmentIndex candidate = EquipmentIndex.WeaponItemBeginSlot;
                 candidate < EquipmentIndex.ExtraWeaponSlot;
                 candidate++)
            {
                MissionWeapon weapon = agent.Equipment[candidate];
                if (weapon.IsEmpty ||
                    !weapon.IsAnyConsumable() ||
                    weapon.ModifiedMaxAmount != PartialFixtureMaximumAmount)
                {
                    continue;
                }

                if (!objectManager.TryGetId(weapon.Item, out itemObjectId))
                {
                    error = "consumable-item-unregistered";
                    return false;
                }

                sourceSlot = candidate;
                return true;
            }

            error = "no-32-stack-consumable";
            return false;
        }

        private bool TryApplyPartialConsumableFixture(
            CoopAgentInfo agentInfo,
            EquipmentIndex sourceSlot,
            EquipmentIndex dropSlot,
            string itemObjectId,
            short sourceAmount,
            short droppedAmount,
            bool locallyControlled,
            out string error)
        {
            error = null;
            if (partialConsumableFixtures.ContainsKey(agentInfo.AgentId))
            {
                error = "partial-fixture-active";
                return false;
            }

            Agent agent = agentInfo.Agent;
            if (agent == null || agent.Mission != Mission.Current || !agent.IsActive())
            {
                error = "agent-unavailable";
                return false;
            }
            if (sourceSlot < EquipmentIndex.WeaponItemBeginSlot ||
                sourceSlot >= EquipmentIndex.ExtraWeaponSlot ||
                dropSlot != EquipmentIndex.ExtraWeaponSlot)
            {
                error = "invalid-fixture-slots";
                return false;
            }

            MissionWeapon sourceWeapon = agent.Equipment[sourceSlot];
            MissionWeapon dropWeapon = agent.Equipment[dropSlot];
            if (sourceWeapon.IsEmpty ||
                !sourceWeapon.IsAnyConsumable() ||
                sourceWeapon.ModifiedMaxAmount != PartialFixtureMaximumAmount)
            {
                error = "source-consumable-unavailable";
                return false;
            }
            if (!dropWeapon.IsEmpty)
            {
                error = "drop-slot-not-empty";
                return false;
            }
            if (!objectManager.TryGetId(sourceWeapon.Item, out string actualItemObjectId) ||
                actualItemObjectId != itemObjectId)
            {
                error = "source-item-mismatch";
                return false;
            }
            if (sourceAmount <= 0 ||
                sourceAmount >= sourceWeapon.ModifiedMaxAmount ||
                droppedAmount <= sourceWeapon.ModifiedMaxAmount - sourceAmount ||
                droppedAmount > sourceWeapon.ModifiedMaxAmount)
            {
                error = "invalid-partial-amounts";
                return false;
            }

            PartialConsumableFixtureSnapshot snapshot = new PartialConsumableFixtureSnapshot
            {
                Agent = agent,
                AgentId = agentInfo.AgentId,
                SourceSlot = sourceSlot,
                DropSlot = dropSlot,
                SourceWeapon = sourceWeapon,
                DropWeapon = dropWeapon,
                Equipment = new AgentEquipmentData(agent),
                LocallyControlled = locallyControlled,
                ControllerId = agentInfo.CurrentAuthority,
            };
            partialConsumableFixtures.Add(agentInfo.AgentId, snapshot);

            try
            {
                MissionWeapon duplicateWeapon = sourceWeapon;
                duplicateWeapon.Amount = droppedAmount;
                agent.EquipWeaponWithNewEntity(dropSlot, ref duplicateWeapon);
                agent.SetWeaponAmountInSlot(sourceSlot, sourceAmount, enforcePrimaryItem: true);
                return true;
            }
            catch (Exception exception)
            {
                Logger.Error(
                    exception,
                    "Failed to stage partial consumable fixture for {AgentId}",
                    agentInfo.AgentId);
                try
                {
                    RestoreFixtureSnapshot(snapshot);
                    if (!FixtureSlotMatches(agent.Equipment[sourceSlot], sourceWeapon) ||
                        !FixtureSlotMatches(agent.Equipment[dropSlot], dropWeapon))
                    {
                        error = "fixture-staging-failed-rollback-incomplete";
                        return false;
                    }

                    partialConsumableFixtures.Remove(agentInfo.AgentId);
                    error = "fixture-staging-failed-rolled-back";
                }
                catch (Exception rollbackException)
                {
                    Logger.Error(
                        rollbackException,
                        "Failed to roll back partial consumable fixture for {AgentId}",
                        agentInfo.AgentId);
                    error = "fixture-staging-failed-rollback-threw";
                }
                return false;
            }
        }

        private bool TryRestorePartialConsumableFixture(
            Guid agentId,
            Guid worldItemId,
            out string error,
            out short sourceAmount,
            out bool dropSlotEmpty,
            out bool worldItemInactive)
        {
            error = null;
            sourceAmount = 0;
            dropSlotEmpty = false;
            worldItemInactive = false;

            if (!partialConsumableFixtures.TryGetValue(agentId, out PartialConsumableFixtureSnapshot snapshot))
            {
                error = "partial-fixture-not-found";
                return false;
            }
            if (snapshot.Agent == null ||
                snapshot.Agent.Mission != Mission.Current ||
                !snapshot.Agent.IsActive())
            {
                if (TryGetAvailableWorldItem(worldItemId, out SpawnedItemEntity unavailableWorldItem) &&
                    !TryRemoveWorldItem(unavailableWorldItem))
                {
                    error = "agent-unavailable-world-item-removal-failed";
                    return false;
                }

                worldItemInactive = !TryGetAvailableWorldItem(worldItemId, out _);
                if (!worldItemInactive)
                {
                    error = "agent-unavailable-world-item-still-active";
                    return false;
                }
                if (worldItemId != Guid.Empty)
                    worldItemRegistry.Remove(worldItemId);
                partialConsumableFixtures.Remove(agentId);
                dropSlotEmpty = true;
                return true;
            }

            if (TryGetAvailableWorldItem(worldItemId, out SpawnedItemEntity worldItem) &&
                !TryRemoveWorldItem(worldItem))
            {
                error = "world-item-removal-failed";
                return false;
            }
            worldItemInactive = !TryGetAvailableWorldItem(worldItemId, out _);
            if (!worldItemInactive)
            {
                error = "world-item-still-active";
                return false;
            }
            if (worldItemId != Guid.Empty)
                worldItemRegistry.Remove(worldItemId);

            RestoreFixtureSnapshot(snapshot);

            sourceAmount = snapshot.Agent.Equipment[snapshot.SourceSlot].Amount;
            dropSlotEmpty = snapshot.Agent.Equipment[snapshot.DropSlot].IsEmpty;
            if (!FixtureSlotMatches(snapshot.Agent.Equipment[snapshot.SourceSlot], snapshot.SourceWeapon) ||
                !FixtureSlotMatches(snapshot.Agent.Equipment[snapshot.DropSlot], snapshot.DropWeapon))
            {
                error = "original-slots-not-restored";
                return false;
            }

            partialConsumableFixtures.Remove(agentId);
            return true;
        }

        private void TrackPartialConsumableFixtureWorldItemSend(MessagePayload<WeaponDropped> payload)
        {
            if (payload.What.DroppedItem == null ||
                !networkAgentRegistry.TryGetAgentInfo(payload.What.Agent, out CoopAgentInfo agentInfo) ||
                !partialConsumableFixtures.TryGetValue(
                    agentInfo.AgentId,
                    out PartialConsumableFixtureSnapshot snapshot) ||
                payload.What.EquipmentIndex != snapshot.DropSlot)
            {
                return;
            }

            snapshot.WorldItemId = worldItemRegistry.GetOrCreateId(payload.What.DroppedItem);
        }

        private void TrackPartialConsumableFixtureWorldItemReceive(
            MessagePayload<NetworkWeaponDropped> payload)
        {
            GameThread.RunSafe(() =>
            {
                if (partialConsumableFixtures.TryGetValue(
                        payload.What.AgentId,
                        out PartialConsumableFixtureSnapshot snapshot) &&
                    payload.What.EquipmentIndex == snapshot.DropSlot)
                {
                    snapshot.WorldItemId = payload.What.WorldItemId;
                }
            });
        }

        private void HandleFixtureOwnerLeft(MessagePayload<MissionPeerLeft> payload)
        {
            CleanupPartialConsumableFixtures(payload.What.ControllerId);
        }

        private void HandleFixtureOwnerDisconnected(MessagePayload<MissionPeerDisconnected> payload)
        {
            CleanupPartialConsumableFixtures(payload.What.ControllerId);
        }

        private void CleanupPartialConsumableFixtures(string controllerId)
        {
            if (string.IsNullOrEmpty(controllerId)) return;

            GameThread.RunSafe(() =>
            {
                var agentIds = new List<Guid>();
                foreach (KeyValuePair<Guid, PartialConsumableFixtureSnapshot> fixture in partialConsumableFixtures)
                {
                    if (fixture.Value.ControllerId == controllerId)
                        agentIds.Add(fixture.Key);
                }

                foreach (Guid agentId in agentIds)
                {
                    if (!partialConsumableFixtures.TryGetValue(
                            agentId,
                            out PartialConsumableFixtureSnapshot snapshot))
                    {
                        continue;
                    }

                    if (!TryRestorePartialConsumableFixture(
                            agentId,
                            snapshot.WorldItemId,
                            out string error,
                            out _,
                            out _,
                            out _))
                    {
                        Logger.Error(
                            "Failed to clean partial consumable fixture for departed controller {ControllerId}, agent {AgentId}: {Error}",
                            controllerId,
                            agentId,
                            error);
                    }
                }
            });
        }

        private bool TryGetAvailableWorldItem(Guid worldItemId, out SpawnedItemEntity worldItem)
        {
            worldItem = null;
            return worldItemId != Guid.Empty &&
                worldItemRegistry.TryGet(worldItemId, out worldItem) &&
                IsWorldItemAvailable(worldItem);
        }

        private static bool TryRemoveWorldItem(SpawnedItemEntity worldItem)
        {
            if (!IsWorldItemAvailable(worldItem)) return true;
            if (!worldItem.GameEntity.IsValid) return false;

            worldItem.GameEntity.Remove(0);
            return !IsWorldItemAvailable(worldItem);
        }

        private static void RestoreFixtureSnapshot(PartialConsumableFixtureSnapshot snapshot)
        {
            if (snapshot?.Agent == null) return;
            RestoreFixtureSlot(snapshot.Agent, snapshot.SourceSlot, snapshot.SourceWeapon);
            RestoreFixtureSlot(snapshot.Agent, snapshot.DropSlot, snapshot.DropWeapon);
            snapshot.Equipment.Apply(snapshot.Agent);
        }

        private static void RestoreFixtureSlot(
            Agent agent,
            EquipmentIndex slot,
            MissionWeapon originalWeapon)
        {
            if (originalWeapon.IsEmpty)
            {
                if (!agent.Equipment[slot].IsEmpty)
                    agent.RemoveEquippedWeapon(slot);
                return;
            }

            MissionWeapon restoredWeapon = originalWeapon;
            agent.EquipWeaponWithNewEntity(slot, ref restoredWeapon);
        }

        private static bool FixtureSlotMatches(MissionWeapon actual, MissionWeapon expected)
        {
            if (expected.IsEmpty) return actual.IsEmpty;
            return !actual.IsEmpty &&
                ReferenceEquals(actual.Item, expected.Item) &&
                ReferenceEquals(actual.ItemModifier, expected.ItemModifier) &&
                actual.Amount == expected.Amount;
        }

#endif
    }
}
