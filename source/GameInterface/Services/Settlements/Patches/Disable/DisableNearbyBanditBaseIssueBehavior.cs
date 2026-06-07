using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.Settlements.Patches.Disable;


[HarmonyPatch(typeof(NearbyBanditBaseIssueBehavior))]
internal class DisableNearbyBanditBaseIssueBehavior
{
    [HarmonyPatch(nameof(NearbyBanditBaseIssueBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
