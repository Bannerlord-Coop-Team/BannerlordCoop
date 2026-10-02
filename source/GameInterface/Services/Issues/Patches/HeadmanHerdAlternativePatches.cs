using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

using Issue = HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue;

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithAlternativeSolution))]
internal static class HeadmanHerdAlternativeCompletionPatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    internal static bool Prefix(IssueBase __instance, out (IDisposable Owner, IDisposable Completion) __state)
    {
        __state = default;
        if (__instance is not Issue || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ModInformation.IsClient || !__instance.IsSolvingWithAlternative
            || !__instance.AlternativeSolutionReturnTimeForTroops.IsPast) return false;
        if (!ContainerProvider.TryResolve<IHeadmanHerdQuestAuthority>(out var authority)
            || !authority.TryEnter(__instance.IssueOwner, out var ownerScope)) return false;
        __state = (ownerScope, new AlternativeSolutionCompletionAuthorityGuard());
        return true;
    }

    [HarmonyFinalizer]
    private static void Finalizer((IDisposable Owner, IDisposable Completion) __state)
    {
        __state.Completion?.Dispose();
        __state.Owner?.Dispose();
    }
}

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithCancel))]
internal static class HeadmanHerdIssueCancellationPatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    internal static bool Prefix(IssueBase __instance, out IDisposable __state)
    {
        __state = null;
        if (__instance is not Issue || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ModInformation.IsClient) return IssueFinalizeAuthorityGuard.IsActive;
        if (!__instance.IsTriedToSolveBefore)
        {
            __state = new IssueFinalizeAuthorityGuard();
            return true;
        }
        return ContainerProvider.TryResolve<IHeadmanHerdQuestAuthority>(out var authority)
            && authority.TryEnter(__instance.IssueOwner, out __state);
    }

    [HarmonyFinalizer]
    private static void Finalizer(IDisposable __state) => __state?.Dispose();
}

[HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnIssueUpdated))]
internal static class HeadmanHerdAlternativeUpdatedPatch
{
    [HarmonyPostfix]
    private static void Postfix(IssueBase issue, IssueBase.IssueUpdateDetails details)
    {
        if (ModInformation.IsClient || CallOriginalPolicy.IsOriginalAllowed()
            || issue is not Issue herd || !issue.IsSolvingWithAlternative) return;
        if (details == IssueBase.IssueUpdateDetails.SentTroopsFinishedQuest)
            IssueManagerQuestCompletedReasonCapture.PendingReasons[issue.IssueOwner] = IssueFinalizeReason.AlternativeSolutionSuccess;
        else if (details == IssueBase.IssueUpdateDetails.SentTroopsFailedQuest)
            IssueManagerQuestCompletedReasonCapture.PendingReasons[issue.IssueOwner] = IssueFinalizeReason.AlternativeSolutionFailure;
        else if (details == IssueBase.IssueUpdateDetails.IssueCancel)
            IssueManagerQuestCompletedReasonCapture.PendingReasons[issue.IssueOwner] = IssueFinalizeReason.QuestCancel;
        MessageBroker.Instance.Publish(herd, new HeadmanHerdAlternativeUpdated(herd, details));
    }
}

[HarmonyPatch(typeof(IssueManager), nameof(IssueManager.DailyTick))]
internal static class HeadmanHerdAlternativeProgressPatch
{
    [HarmonyPostfix]
    private static void Postfix(IssueManager __instance)
    {
        if (ModInformation.IsClient || CallOriginalPolicy.IsOriginalAllowed()) return;
        foreach (var entry in __instance.Issues)
            if (entry.Value is Issue { IsSolvingWithAlternative: true } issue)
                MessageBroker.Instance.Publish(issue, new HeadmanHerdAlternativeUpdated(issue));
    }
}

[HarmonyPatch(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.OnIssueUpdated))]
internal static class HeadmanHerdIssueUpdatedConsequencePatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase issue) => issue is not Issue || ModInformation.IsServer
        || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate();
}
