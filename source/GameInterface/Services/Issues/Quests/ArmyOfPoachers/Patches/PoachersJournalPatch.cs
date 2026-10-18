using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssueQuest;
using Issue = TaleWorlds.CampaignSystem.Issues.MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssue;

namespace GameInterface.Services.Issues.Quests.ArmyOfPoachers.Patches;

[HarmonyPatch]
internal class PoachersJournalPatch
{
    [HarmonyPatch(typeof(QuestBase), nameof(QuestBase.AddLog))]
    [HarmonyPostfix]
    private static void Postfix(QuestBase __instance, TextObject text)
    {
        if (__instance is Quest && ContainerProvider.TryResolve<PoachersJournalHandler>(out var journals))
            journals.SendLog(__instance, text);
    }

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.AddLog))]
    [HarmonyPostfix]
    private static void AlternativeLogPostfix(IssueBase __instance, JournalLog log)
    {
        // The owner's own daily tick already adds the returning log
        if (__instance is Issue && __instance.IsSolvingWithAlternative &&
            log.LogText?.Value != __instance.AlternativeSolutionEndLogDefault.Value &&
            ContainerProvider.TryResolve<PoachersJournalHandler>(out var journals))
            journals.SendLog(__instance, log.LogText);
    }

    [HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnIssueUpdated))]
    [HarmonyPostfix]
    private static void AlternativeUpdatePostfix(IssueBase issue, IssueBase.IssueUpdateDetails details)
    {
        if (issue is Issue && issue.IsSolvingWithAlternative && ContainerProvider.TryResolve<PoachersJournalHandler>(out var journals))
            journals.SendLog(issue, null, (int)details);
    }
}
