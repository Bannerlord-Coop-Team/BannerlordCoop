using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.Towns.Patches.Disabled;

[HarmonyPatch(typeof(LadysKnightOutIssueBehavior))]
internal class DisableLadysKnightOutIssueBehavior
{
    [HarmonyPatch(nameof(LadysKnightOutIssueBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
