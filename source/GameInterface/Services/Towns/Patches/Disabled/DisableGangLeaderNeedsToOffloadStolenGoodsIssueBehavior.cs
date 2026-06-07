using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.Towns.Patches.Disabled;

[HarmonyPatch(typeof(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior))]
internal class DisableGangLeaderNeedsToOffloadStolenGoodsIssueBehavior
{
    [HarmonyPatch(nameof(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
