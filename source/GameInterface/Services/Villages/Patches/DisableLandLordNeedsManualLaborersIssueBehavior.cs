using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(LandLordNeedsManualLaborersIssueBehavior))]
internal class DisableLandLordNeedsManualLaborersIssueBehavior
{
    [HarmonyPatch(nameof(LandLordNeedsManualLaborersIssueBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
