using Common.Messaging;
using HarmonyLib;
using Missions.Agents.Messages;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Patches
{
    public class WeaponPickupPatch
    {
        [HarmonyPatch(typeof(Agent), "EquipWeaponFromSpawnedItemEntity")]
        public class WeaponPickupHandlerPatch
        {
            static void Postfix(ref Agent __instance, EquipmentIndex slotIndex, SpawnedItemEntity spawnedItemEntity, bool removeWeapon)
            {
                WeaponPickedup message = new WeaponPickedup(__instance, slotIndex, spawnedItemEntity.WeaponCopy.Item, spawnedItemEntity.WeaponCopy.ItemModifier, spawnedItemEntity.WeaponCopy.Banner);

                MessageBroker.Instance.Publish(__instance, message);
            }
        }
    }
}
