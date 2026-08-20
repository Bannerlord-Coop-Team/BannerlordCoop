using Common.Messaging;
using Common.Util;
using HarmonyLib;
using Missions.Agents.Messages;
using Missions.Agents.Packets;
using Missions.Tournaments;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Patches
{
    /// <summary>
    /// Patch on ItemPickups for WeaponPickupHandler
    /// </summary>
    [HarmonyPatch(typeof(Agent), "OnItemPickup")]
    [HarmonyPatchCategory(MissionModule.WeaponPickupPatchCategory)]
    public class AgentPickupPatch
    {
        private readonly struct PickupState
        {
            public EquipmentIndex EquipmentIndex { get; }
            public short SlotAmount { get; }
            public short WorldItemAmount { get; }

            public PickupState(
                EquipmentIndex equipmentIndex,
                short slotAmount,
                short worldItemAmount)
            {
                EquipmentIndex = equipmentIndex;
                SlotAmount = slotAmount;
                WorldItemAmount = worldItemAmount;
            }
        }

        static void Prefix(
            SpawnedItemEntity spawnedItemEntity,
            EquipmentIndex weaponPickUpSlotIndex,
            Agent __instance,
            out PickupState __state)
        {
            if (AllowedThread.IsThisThreadAllowed())
            {
                __state = default;
                return;
            }

            EquipmentIndex equipmentIndex = weaponPickUpSlotIndex;
            if (equipmentIndex == EquipmentIndex.None)
            {
                equipmentIndex = MissionEquipment.SelectWeaponPickUpSlot(
                    __instance,
                    spawnedItemEntity.WeaponCopy,
                    spawnedItemEntity.IsStuckMissile());
            }

            __state = new PickupState(
                equipmentIndex,
                GetSlotAmount(__instance, equipmentIndex),
                spawnedItemEntity.WeaponCopy.Amount);
        }

        static void Postfix(
            SpawnedItemEntity spawnedItemEntity,
            Agent __instance,
            bool removeWeapon,
            PickupState __state)
        {
            if (AllowedThread.IsThisThreadAllowed()) return;

            CoopTournamentController controller = Mission.Current?.GetMissionBehavior<CoopTournamentController>();
            if (controller?.IsSpectatorAgent(__instance) == true) return;

            MissionWeapon weapon = spawnedItemEntity.WeaponCopy;
            MissionWeapon resultingSlotWeapon = GetSlotWeapon(__instance, __state.EquipmentIndex);
            WeaponPickedup message = new WeaponPickedup(
                __instance,
                spawnedItemEntity,
                __state.EquipmentIndex,
                weapon.Item,
                weapon.ItemModifier,
                weapon.Banner,
                new AgentEquipmentData(__instance),
                __state.SlotAmount,
                __state.WorldItemAmount,
                GetSlotAmount(__instance, __state.EquipmentIndex),
                weapon.Amount,
                removeWeapon,
                resultingSlotWeapon);
            MessageBroker.Instance.Publish(__instance, message);
        }

        private static short GetSlotAmount(Agent agent, EquipmentIndex equipmentIndex)
        {
            if (equipmentIndex < EquipmentIndex.WeaponItemBeginSlot ||
                equipmentIndex >= EquipmentIndex.NumAllWeaponSlots)
                return 0;

            MissionWeapon weapon = agent.Equipment[equipmentIndex];
            return weapon.IsEmpty ? (short)0 : weapon.Amount;
        }

        private static MissionWeapon GetSlotWeapon(Agent agent, EquipmentIndex equipmentIndex)
        {
            if (equipmentIndex < EquipmentIndex.WeaponItemBeginSlot ||
                equipmentIndex >= EquipmentIndex.NumAllWeaponSlots)
                return default;

            return agent.Equipment[equipmentIndex];
        }
    }
}
