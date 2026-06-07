using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(LandlordNeedsAccessToVillageCommonsIssueBehavior))]
internal class DisableLandlordNeedsAccessToVillageCommonsIssueBehavior
{
    [HarmonyPatch(nameof(LandlordNeedsAccessToVillageCommonsIssueBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
