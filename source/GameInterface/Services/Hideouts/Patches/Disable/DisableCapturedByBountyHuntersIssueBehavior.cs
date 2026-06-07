using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.Hideouts.Patches.Disable;

[HarmonyPatch(typeof(CapturedByBountyHuntersIssueBehavior))]
internal class DisableCapturedByBountyHuntersIssueBehavior
{
    [HarmonyPatch(nameof(CapturedByBountyHuntersIssueBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
