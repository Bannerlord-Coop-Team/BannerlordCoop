using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

[HarmonyPatch(typeof(IssueManager))]
internal class IssueManagerDisablePatches
{
    [HarmonyPatch(nameof(IssueManager.DailyTick))]
    [HarmonyPrefix]
    private static bool DisableIssueDailyTick() => false;

    [HarmonyPatch(nameof(IssueManager.HourlyTick))]
    [HarmonyPrefix]
    private static bool DisableIssueHourlyTick() => false;
}
