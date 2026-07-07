using Common;
using GameInterface.Policies;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch]
internal class VillageForceActionSuccessConsequencePatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(VillageHostileActionCampaignBehavior), "village_force_supplies_ended_successfully_on_consequence");
        yield return AccessTools.Method(typeof(VillageHostileActionCampaignBehavior), "village_force_volunteers_ended_successfully_on_consequence");
    }

    [HarmonyPrefix]
    private static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;

        return false;
    }
}