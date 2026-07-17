using Common.Messaging;
using HarmonyLib;
using Missions.Agents.Messages;
using Missions.Tournaments;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Patches
{
    /// <summary>
    /// Patch on ItemPickups for WeaponPickupHandler
    /// </summary>
    [HarmonyPatch(typeof(Agent), "OnItemPickup")]
    public class AgentPickupPatch
    {
        static void Postfix(SpawnedItemEntity spawnedItemEntity, EquipmentIndex weaponPickUpSlotIndex, Agent __instance)
        {
            CoopTournamentController controller = Mission.Current?.GetMissionBehavior<CoopTournamentController>();
            if (controller?.IsSpectatorAgent(__instance) == true) return;

            MissionWeapon weapon = spawnedItemEntity.WeaponCopy;
            WeaponPickedup message = new WeaponPickedup(__instance, weaponPickUpSlotIndex, weapon.Item, weapon.ItemModifier, weapon.Banner);
            MessageBroker.Instance.Publish(__instance, message);
        }
    }
}
