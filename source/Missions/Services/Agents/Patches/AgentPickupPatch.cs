using Common.Messaging;
using HarmonyLib;
using Missions.Services.Agents.Messages;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Services.Agents.Patches
{
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
