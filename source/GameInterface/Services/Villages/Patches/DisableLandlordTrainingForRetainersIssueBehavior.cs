using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(LandlordTrainingForRetainersIssueBehavior))]
internal class DisableLandlordTrainingForRetainersIssueBehavior
{
    [HarmonyPatch(nameof(LandlordTrainingForRetainersIssueBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
