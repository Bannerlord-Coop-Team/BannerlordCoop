using HarmonyLib;
using SandBox.CampaignBehaviors;
using GameInterface.Policies;

namespace GameInterface.Services.CampaignService.Patches;

[HarmonyPatch(typeof(StatisticsCampaignBehavior))]
internal class DisableStatisticsCampaignBehavior
{
    [HarmonyPatch(nameof(StatisticsCampaignBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
