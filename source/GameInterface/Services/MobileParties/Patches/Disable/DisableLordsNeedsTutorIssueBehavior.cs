using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(LordsNeedsTutorIssueBehavior))]
internal class DisableLordsNeedsTutorIssueBehavior
{
    [HarmonyPatch(nameof(LordsNeedsTutorIssueBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
