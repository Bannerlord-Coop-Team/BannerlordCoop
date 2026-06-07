using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using GameInterface.Policies;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch(typeof(IssueManager))]
internal class IssueManagerDisablePatches
{
    [HarmonyPatch(nameof(IssueManager.DailyTick))]
    [HarmonyPrefix]
    private static bool DisableIssueDailyTick()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }

    [HarmonyPatch(nameof(IssueManager.HourlyTick))]
    [HarmonyPrefix]
    private static bool DisableIssueHourlyTick()
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return true;
        return false;
    }
}
