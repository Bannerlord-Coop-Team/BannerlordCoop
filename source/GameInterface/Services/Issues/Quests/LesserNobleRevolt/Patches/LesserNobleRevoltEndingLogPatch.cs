using Common;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using Issue = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssueQuest;

namespace GameInterface.Services.Issues.Quests.LesserNobleRevolt.Patches;

// Every server ending reaches IssueFinalized inside CompleteQuestWith*, after vanilla's AddLog and before the
// framework's postfix announces the ending, so the owner gets the line before its generic replay.
[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueFinalized))]
internal static class LesserNobleRevoltEndingLogPatch
{
    [HarmonyPrefix]
    private static void Prefix(IssueBase __instance)
    {
        if (ModInformation.IsServer && __instance is Issue && __instance.IssueQuest is Quest quest &&
            ContainerProvider.TryResolve<ILesserNobleRevoltWorldEventService>(out var service))
        {
            service.SendEndingLog(__instance, quest);
        }
    }
}
