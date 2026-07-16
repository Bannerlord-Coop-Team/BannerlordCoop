using Common.Messaging;
using HarmonyLib;
using Missions.Agents.Messages;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Patches
{
    public class WeaponDropPatch
    {

        [HarmonyPatch(typeof(Agent), "DropItem")]
        public class WeaponDropHandlerPatch
        {
            static void Prefix(out HashSet<SpawnedItemEntity> __state)
            {
                __state = WeaponDropItemTracker.Capture();
            }

            static void Postfix(
                ref Agent __instance,
                EquipmentIndex itemIndex,
                WeaponClass pickedUpItemType,
                HashSet<SpawnedItemEntity> __state)
            {
                SpawnedItemEntity droppedItem = WeaponDropItemTracker.FindDroppedItem(__state);
                WeaponDropped message = new WeaponDropped(__instance, itemIndex, droppedItem);

                //Commented out as missiles are not functional yet
                MessageBroker.Instance.Publish(__instance, message);
            }
        }
    }
}
