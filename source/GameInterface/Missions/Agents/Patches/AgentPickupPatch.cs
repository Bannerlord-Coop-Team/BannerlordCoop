using Common.Messaging;
using GameInterface.Missions.Agents.Messages;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Missions.Agents.Patches
{
    /// <summary>
    /// Patch on ItemPickups for WeaponPickupHandler
    /// </summary>
    [HarmonyPatch(typeof(Agent), "OnItemPickup")]
    public class AgentPickupPatch
    {
        static void Postfix(SpawnedItemEntity spawnedItemEntity, EquipmentIndex weaponPickUpSlotIndex, Agent __instance)
        {
            MissionWeapon weapon = spawnedItemEntity.WeaponCopy;
            WeaponPickedup message = new WeaponPickedup(__instance, weaponPickUpSlotIndex, weapon.Item, weapon.ItemModifier, weapon.Banner);
            MessageBroker.Instance.Publish(__instance, message);
        }
    }
}
