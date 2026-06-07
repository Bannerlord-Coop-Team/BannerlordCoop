using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.Arenas.Patches;

[HarmonyPatch(typeof(BettingFraudIssueBehavior))]
internal class DisableBettingFraudIssueBehavior
{
    [HarmonyPatch(nameof(BettingFraudIssueBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
