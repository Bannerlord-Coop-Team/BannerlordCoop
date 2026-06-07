using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(LandLordCompanyOfTroubleIssueBehavior))]
internal class DisableLandLordCompanyOfTroubleIssueBehavior
{
    [HarmonyPatch(nameof(LandLordCompanyOfTroubleIssueBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
