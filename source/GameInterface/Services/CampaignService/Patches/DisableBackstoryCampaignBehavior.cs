using HarmonyLib;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.CampaignService.Patches;

[HarmonyPatch(typeof(BackstoryCampaignBehavior))]
internal class DisableBackstoryCampaignBehavior
{
    [HarmonyPatch(nameof(BackstoryCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
