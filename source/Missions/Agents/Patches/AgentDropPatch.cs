using Common.Messaging;
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
    public class AgentDropPatch
    {
        static void Prefix(out HashSet<SpawnedItemEntity> __state)
        {
            __state = WeaponDropItemTracker.Capture();
        }

        static void Postfix(
            EquipmentIndex itemIndex,
            Agent __instance,
            HashSet<SpawnedItemEntity> __state)
        {
            SpawnedItemEntity droppedItem = WeaponDropItemTracker.FindDroppedItem(__state);
            WeaponDropped message = new WeaponDropped(__instance, itemIndex, droppedItem);
            MessageBroker.Instance.Publish(__instance, message);
        }
    }
}
