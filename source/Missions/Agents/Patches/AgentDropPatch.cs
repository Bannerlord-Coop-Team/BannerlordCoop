using Common.Messaging;
using Common.Util;
using HarmonyLib;
using Missions.Agents.Messages;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Patches
{
    /// <summary>
    /// Patch on DropItem for WeaponDropHandler
    /// </summary>
    [HarmonyPatch(typeof(Agent), "DropItem")]
    [HarmonyPatchCategory(MissionModule.WeaponDropPatchCategory)]
    public class AgentDropPatch
    {
        private readonly struct DropState
        {
            public HashSet<SpawnedItemEntity> ExistingItems { get; }
            public MissionWeapon Weapon { get; }

            public DropState(
                HashSet<SpawnedItemEntity> existingItems,
                MissionWeapon weapon)
            {
                ExistingItems = existingItems;
                Weapon = weapon;
            }
        }

        static void Prefix(
            EquipmentIndex itemIndex,
            Agent __instance,
            out DropState __state)
        {
            if (AllowedThread.IsThisThreadAllowed())
            {
                __state = default;
                return;
            }

            MissionWeapon weapon = default;
            if (__instance != null &&
                itemIndex >= EquipmentIndex.WeaponItemBeginSlot &&
                itemIndex < EquipmentIndex.NumAllWeaponSlots)
            {
                weapon = __instance.Equipment[itemIndex];
            }

            __state = new DropState(WeaponDropItemTracker.Capture(), weapon);
        }

        static void Postfix(
            EquipmentIndex itemIndex,
            Agent __instance,
            DropState __state)
        {
            if (AllowedThread.IsThisThreadAllowed() || __state.ExistingItems == null) return;

            SpawnedItemEntity droppedItem = WeaponDropItemTracker.FindDroppedItem(__state.ExistingItems);
            WeaponDropped message = new WeaponDropped(
                __instance,
                itemIndex,
                __state.Weapon,
                droppedItem);
            MessageBroker.Instance.Publish(__instance, message);
        }
    }
}
