using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.MobileParties.Patches;

[HarmonyPatch(typeof(LordWantsRivalCapturedIssueBehavior))]
internal class DisableLordWantsRivalCapturedIssueBehavior
{
    [HarmonyPatch(nameof(LordWantsRivalCapturedIssueBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
