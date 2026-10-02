using Common;
using GameInterface.Policies;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

using Issue = HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue;
using Quest = HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssueQuest;

[HarmonyPatch(typeof(Quest), nameof(Quest.QuestAcceptedConsequences))]
internal static class HeadmanHerdAcceptedConsequencePatch
{
    [HarmonyPrefix]
    private static bool Prefix() => CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()
        || ModInformation.IsServer && QuestSolutionStartAuthorityGuard.IsActive;
}

[HarmonyPatch(typeof(QuestBase), nameof(QuestBase.StartQuest))]
internal static class HeadmanHerdPersonalQuestStartPatch
{
    [HarmonyPrefix]
    private static void Prefix(QuestBase __instance)
    {
        if (__instance is not Quest quest || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return;
        if (quest.QuestGiver.Issue is Issue issue
            && ContainerProvider.TryResolve<IHeadmanHerdPersonalOwnership>(out var ownership))
            ownership.RecordCurrentOwner(issue, quest);
    }
}

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.StartIssueWithAlternativeSolution))]
internal static class HeadmanHerdPersonalAlternativeStartPatch
{
    [HarmonyPrefix]
    private static void Prefix(IssueBase __instance)
    {
        if (__instance is not Issue issue || ModInformation.IsClient || !AlternativeSolutionStartAuthorityGuard.IsActive) return;
        if (ContainerProvider.TryResolve<IHeadmanHerdPersonalOwnership>(out var ownership))
            ownership.RecordCurrentOwner(issue);
    }
}
