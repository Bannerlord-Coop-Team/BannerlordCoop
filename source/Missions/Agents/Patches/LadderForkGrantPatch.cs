using Common.Messaging;
using Common.Util;
using HarmonyLib;
using Missions.Agents.Messages;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace Missions.Agents.Patches;

[HarmonyPatch(typeof(Agent), nameof(Agent.EquipWeaponToExtraSlotAndWield))]
[HarmonyPatchCategory(MissionModule.WeaponPickupPatchCategory)]
public class LadderForkGrantPatch
{
    private static void Prefix(Agent __instance, ref MissionWeapon weapon, out bool __state)
    {
        __state = !AllowedThread.IsThisThreadAllowed() &&
            IsLadderForkGrant(__instance, weapon);
    }

    internal static bool IsLadderForkGrant(Agent agent, MissionWeapon weapon)
    {
        if (Mission.Current == null || agent == null || weapon.IsEmpty || agent.Mission != Mission.Current ||
            !(agent.CurrentlyUsedGameObject is StandingPoint point)) return false;

        return agent.Mission.MissionObjects.OfType<SiegeLadder>().Any(ladder =>
            ReferenceEquals(ladder._forkPickUpStandingPoint, point) &&
            ReferenceEquals(ladder._forkItem, weapon.Item));
    }

    private static void Postfix(Agent __instance, bool __state)
    {
        if (!__state || AllowedThread.IsThisThreadAllowed() ||
            __instance.Equipment[EquipmentIndex.ExtraWeaponSlot].IsEmpty) return;

        MessageBroker.Instance.Publish(__instance, new LadderForkGranted(__instance));
    }
}
