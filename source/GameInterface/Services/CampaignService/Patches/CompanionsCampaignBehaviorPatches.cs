using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.CampaignService.Patches;

[HarmonyPatch(typeof(CompanionsCampaignBehavior))]
internal class CompanionsCampaignBehaviorPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(CompanionsCampaignBehavior.CreateCompanionAndAddToSettlement))]
    private static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
