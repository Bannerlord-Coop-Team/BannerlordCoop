using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.ObjectManager;
using Missions.Agents.Messages;
using Missions.Agents.Packets;
using Serilog;
using System;
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
        readonly INetworkAgentRegistry networkAgentRegistry;
        readonly INetworkWorldItemRegistry worldItemRegistry;
        readonly IBattleNetwork network;
        readonly IMessageBroker messageBroker;
        readonly IObjectManager objectManager;
        readonly static ILogger Logger = LogManager.GetLogger<WeaponPickupHandler>();
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

        }
        ~WeaponPickupHandler()
        {
            Dispose();
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<WeaponPickedup>(WeaponPickupSend);
            messageBroker.Unsubscribe<NetworkWeaponPickedup>(WeaponPickupReceive);
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

            if (!objectManager.TryGetIdWithLogging(payload.WeaponObject, out var itemObjectId))
                return;

            if (payload.WorldItem == null)
            {
                Logger.Error("No world item was found for a weapon pickup");
                return;
            }

            Guid worldItemId;
            if (payload.WorldItem.Id.CreatedAtRuntime)
                worldItemRegistry.TryGetId(payload.WorldItem, out worldItemId);
            else
                worldItemId = worldItemRegistry.GetOrCreateId(payload.WorldItem);

            NetworkWeaponPickedup message = new NetworkWeaponPickedup(
                agentInfo.AgentId,
                payload.EquipmentIndex,
                worldItemId,
                itemObjectId,
                payload.WeaponModifier,
                payload.Banner,
                payload.CurrentEquipment,
                payload.PreviousSlotAmount,
                payload.PreviousWorldItemAmount,
                payload.ResultingSlotAmount,
                payload.ResultingWorldItemAmount);

            network.SendAll(message);
        }
        private void WeaponPickupReceive(MessagePayload<NetworkWeaponPickedup> obj)
        {
            GameThread.RunSafe(() =>
            {
                if (networkAgentRegistry.TryGetAgentInfo(obj.What.AgentId, out var agentInfo) == false)
                {
                    Logger.Warning("No agent found at {guid} in {class}", obj.What.AgentId, typeof(WeaponPickupHandler));
                    return;
                }

                Agent agent = agentInfo.Agent;
                if (agent == null || agent.Mission != Mission.Current || !agent.IsActive()) return;

                if (!objectManager.TryGetObjectWithLogging<ItemObject>(obj.What.ItemObjectId, out var itemObject))
                    return;

                SpawnedItemEntity worldItem = null;
                if (obj.What.WorldItemId != Guid.Empty)
                {
                    if (!TryGetWorldItem(obj.What.WorldItemId, out worldItem))
                        return;
                    if (!ReferenceEquals(worldItem.WeaponCopy.Item, itemObject))
                    {
                        Logger.Error(
                            "World item {WorldItemId} does not contain item {ItemObjectId}",
                            obj.What.WorldItemId,
                            obj.What.ItemObjectId);
                        return;
                    }
                }

                MissionWeapon missionWeapon = new MissionWeapon(
                    itemObject,
                    obj.What.ItemModifier,
                    obj.What.Banner);
                ApplyWeaponPickup(
                    agentInfo,
                    worldItem,
                    obj.What.EquipmentIndex,
                    ref missionWeapon,
                    obj.What.CurrentEquipment,
                    obj.What.PreviousSlotAmount,
                    obj.What.PreviousWorldItemAmount,
                    obj.What.ResultingSlotAmount,
                    obj.What.ResultingWorldItemAmount);
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

            foreach (MissionObject missionObject in Mission.Current.MissionObjects)
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

            Logger.Error("No world item found for weapon pickup {WorldItemId}", worldItemId);
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
            short resultingWorldItemAmount)
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

                if (worldItem == null)
                    ApplyDetachedWeaponPickup(agent, equipmentIndex, ref missionWeapon);
                else
                    ApplyWorldItemPickup(worldItem, agent, isSuccessful: true, (int)equipmentIndex);

                ApplyWeaponAmounts(
                    agent,
                    worldItem,
                    equipmentIndex,
                    resultingSlotAmount,
                    resultingWorldItemAmount);

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
    }
}
