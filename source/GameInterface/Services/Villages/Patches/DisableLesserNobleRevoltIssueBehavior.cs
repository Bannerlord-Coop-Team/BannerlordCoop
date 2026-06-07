using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.Villages.Patches;

[HarmonyPatch(typeof(LesserNobleRevoltIssueBehavior))]
internal class DisableLesserNobleRevoltIssueBehavior
{
    [HarmonyPatch(nameof(LesserNobleRevoltIssueBehavior.RegisterEvents))]
    static bool Prefix()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
