using Common;
using Common.Network;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;
using Issue = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssue;
using Quest = TaleWorlds.CampaignSystem.Issues.HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssueQuest;

namespace GameInterface.Services.Issues.Quests.HeadmanNeedsGrain.Patches;

[HarmonyPatch]
internal class HeadmanNeedsGrainJournalPatches
{
    [HarmonyPatch(typeof(IssueManager), nameof(IssueManager.DailyTick))]
    [HarmonyPostfix]
    private static void DailyTickPostfix()
    {
        if (ModInformation.IsClient || !ContainerProvider.TryResolve<IObjectManager>(out var objects) ||
            !ContainerProvider.TryResolve<INetwork>(out var network)) return;
        foreach (var issue in Campaign.Current.IssueManager.Issues.Values.OfType<Issue>())
        {
            if (issue.IsSolvingWithAlternative && issue.JournalEntries.Count > 0 &&
                objects.TryGetIdWithLogging(issue.IssueOwner, out var giverId))
                network.SendAll(new NetworkHeadmanNeedsGrainProgress(giverId, issue.StringId, issue.JournalEntries[0].CurrentProgress));
        }
    }

    [HarmonyPatch(typeof(QuestBase), nameof(QuestBase.AddLog))]
    [HarmonyPostfix]
    private static void QuestLogPostfix(QuestBase __instance, TextObject text)
    {
        if (ModInformation.IsServer && __instance is Quest)
            Publish(IssueManager.GetIssueOfQuest(__instance), text, true);
    }

    [HarmonyPatch(typeof(IssueBase), nameof(IssueBase.AddLog))]
    [HarmonyPostfix]
    private static void IssueLogPostfix(IssueBase __instance, JournalLog log)
    {
        // The owner's accept replay already creates the first, discrete return-days log.
        if (ModInformation.IsServer && __instance is Issue && __instance.JournalEntries.Count > 1)
            Publish(__instance, log.LogText, false);
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.OnTimedOut))]
    [HarmonyPrefix]
    private static bool TimeoutPrefix() => ModInformation.IsServer;

    private static void Publish(IssueBase issue, TextObject text, bool questLog)
    {
        if (issue == null || text == null) return;
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objects) ||
            !objects.TryGetIdWithLogging(issue.IssueOwner, out var giverId) ||
            !ContainerProvider.TryResolve<INetwork>(out var network)) return;
        network.SendAll(new NetworkHeadmanNeedsGrainLog(giverId, issue.StringId, text, questLog));
    }
}
