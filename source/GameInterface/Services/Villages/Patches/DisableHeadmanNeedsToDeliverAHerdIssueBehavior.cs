using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(HeadmanNeedsToDeliverAHerdIssueBehavior))]
internal class DisableHeadmanNeedsToDeliverAHerdIssueBehavior
{
    [HarmonyPatch(nameof(HeadmanNeedsToDeliverAHerdIssueBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
