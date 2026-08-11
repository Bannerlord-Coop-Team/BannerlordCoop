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
        static void Postfix(SpawnedItemEntity spawnedItemEntity, EquipmentIndex weaponPickUpSlotIndex, Agent __instance)
        {
            if (AllowedThread.IsThisThreadAllowed()) return;

            CoopTournamentController controller = Mission.Current?.GetMissionBehavior<CoopTournamentController>();
            if (controller?.IsSpectatorAgent(__instance) == true) return;

            MissionWeapon weapon = spawnedItemEntity.WeaponCopy;
            WeaponPickedup message = new WeaponPickedup(
                __instance,
                spawnedItemEntity,
                weaponPickUpSlotIndex,
                weapon.Item,
                weapon.ItemModifier,
                weapon.Banner,
                new AgentEquipmentData(__instance));
            MessageBroker.Instance.Publish(__instance, message);
        }
    }
}
