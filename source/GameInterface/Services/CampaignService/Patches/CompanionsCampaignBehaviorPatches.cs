using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.CampaignService.Patches;

[HarmonyPatch(typeof(CompanionsCampaignBehavior))]
internal class CompanionsCampaignBehaviorPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(CompanionsCampaignBehavior.CreateCompanionAndAddToSettlement))]
    private static bool Prefix()
    {
        return false;
    }
}
