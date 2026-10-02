using Common;
using GameInterface.Policies;
using Common.Messaging;
using GameInterface.Services.Issues.Messages;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Patches;

using Issue = ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue;
using Quest = ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssueQuest;

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueFinalized))]
internal class ArtisanLordRefusalJournalPatch
{
    [HarmonyPrefix]
    private static void Prefix(IssueBase __instance)
    {
        if (__instance is not Issue || ModInformation.IsClient || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return;
        // Vanilla announces refusal success after removing the issue and its owner mapping.
        if (ArtisanOverpricedGoodsActionGuard.IsRefusingLordOffer(__instance.IssueOwner))
            MessageBroker.Instance.Publish(__instance, new ArtisanIssueOutcome(__instance, IssueBase.IssueUpdateDetails.IssueFinishedWithSuccess));
    }
}

[HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnQuestCompleted))]
internal class ArtisanQuestTerminalJournalPatch
{
    [HarmonyPrefix]
    private static void Prefix(QuestBase quest)
    {
        if (quest is not Quest || ModInformation.IsClient || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return;
        if (quest.QuestGiver.Issue is Issue issue && issue.IssueQuest == quest)
            MessageBroker.Instance.Publish(issue, new ArtisanIssueOutcome(issue, IssueBase.IssueUpdateDetails.None, questJournal: true));
    }
}

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithAlternativeSolution))]
internal class ArtisanAlternativeCompletionAuthorityPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void Prefix(IssueBase __instance, out AlternativeSolutionCompletionAuthorityGuard __state)
    {
        __state = __instance is Issue && ModInformation.IsServer
            ? new AlternativeSolutionCompletionAuthorityGuard()
            : null;
    }

    [HarmonyFinalizer]
    private static void Finalizer(AlternativeSolutionCompletionAuthorityGuard __state) => __state?.Dispose();
}

[HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnIssueUpdated))]
internal class ArtisanIssueOutcomePatch
{
    [HarmonyPrefix]
    private static void Prefix(IssueBase issue, IssueBase.IssueUpdateDetails details)
    {
        if (issue is not Issue || ModInformation.IsClient || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return;
        if (issue.IssueQuest != null || !issue.IsTriedToSolveBefore) return;
        if (details == IssueBase.IssueUpdateDetails.None || details == IssueBase.IssueUpdateDetails.PlayerSentTroopsToQuest ||
            details == IssueBase.IssueUpdateDetails.PlayerStartedIssueQuestClassicSolution) return;
        if (details == IssueBase.IssueUpdateDetails.SentTroopsFailedQuest)
            IssueManagerQuestCompletedReasonCapture.PendingReasons[issue.IssueOwner] = IssueFinalizeReason.AlternativeSolutionFail;
        else if (details == IssueBase.IssueUpdateDetails.SentTroopsFinishedQuest)
            IssueManagerQuestCompletedReasonCapture.PendingReasons[issue.IssueOwner] = IssueFinalizeReason.AlternativeSolutionSuccess;
        MessageBroker.Instance.Publish(issue, new ArtisanIssueOutcome(issue, details));
    }
}

[HarmonyPatch(typeof(IssueManager), nameof(IssueManager.DailyTick))]
internal class ArtisanAlternativeProgressPatch
{
    [HarmonyPostfix]
    private static void Postfix(IssueManager __instance)
    {
        if (ModInformation.IsClient || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return;
        foreach (var issue in __instance.Issues.Values)
            if (issue is Issue && issue.IsSolvingWithAlternative)
                MessageBroker.Instance.Publish(issue, new ArtisanIssueOutcome(issue, IssueBase.IssueUpdateDetails.None));
    }
}

[HarmonyPatch]
internal class ArtisanIssueTerminationPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithTimedOut));
        yield return AccessTools.Method(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithStayAliveConditionsFailed));
        yield return AccessTools.Method(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithCancel));
        yield return AccessTools.Method(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithFail));
        yield return AccessTools.Method(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithBetrayal));
        yield return AccessTools.Method(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithAiLord));
        yield return AccessTools.Method(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithAlternativeSolution));
    }

    [HarmonyPrefix]
    private static bool Prefix(IssueBase __instance, out IDisposable __state)
    {
        __state = null;
        if (__instance is not Issue || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (ModInformation.IsClient) return IssueFinalizeAuthorityGuard.IsActive && __instance.IssueQuest is Quest;
        if (!__instance.IsTriedToSolveBefore)
        {
            __state = new IssueFinalizeAuthorityGuard();
            return true;
        }
        return ContainerProvider.TryResolve<IArtisanQuestOwnerContext>(out var context) &&
            context.TryEnter(__instance.IssueOwner, out __state);
    }

    [HarmonyFinalizer]
    private static void Finalizer(IDisposable __state) => __state?.Dispose();
}

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.IssueQuestCanBeDuplicated), MethodType.Getter)]
internal class ArtisanQuestDuplicateEligibilityPatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase __instance, ref bool __result)
    {
        if (__instance is not Issue || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (!ContainerProvider.TryResolve<IArtisanQuestOwnerContext>(out var context)) return true;
        __result = !context.HasConflictingIssue(Hero.MainHero);
        return false;
    }
}

[HarmonyPatch(typeof(Issue), nameof(Issue.AlternativeSolutionCondition))]
internal class ArtisanAlternativeEligibilityPatch
{
    [HarmonyPrefix]
    private static bool Prefix(Issue __instance, ref TextObject explanation, ref bool __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate() || ModInformation.IsClient) return true;
        if (__instance.CheckPreconditions(__instance.IssueOwner, out explanation)) return true;
        __result = false;
        return false;
    }
}

[HarmonyPatch]
internal class ArtisanQuestFinalizationPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithSuccess));
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithFail));
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithCancel));
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithBetrayal));
        yield return AccessTools.Method(typeof(QuestBase), nameof(QuestBase.CompleteQuestWithTimeOut));
    }

    [HarmonyPrefix]
    private static bool Prefix(QuestBase __instance, MethodBase __originalMethod, out IDisposable __state)
    {
        __state = null;
        if (__instance is not Quest || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        if (!__instance.IsOngoing) return false;
        if (ArtisanOrphanLoadCleanupPatch.IsActive && __originalMethod.Name == nameof(QuestBase.CompleteQuestWithCancel) &&
            !Campaign.Current.IssueManager.Issues.Values.Any(issue => issue?.IssueQuest == __instance)) return true;
        if (ModInformation.IsClient && !IssueFinalizeAuthorityGuard.IsActive) return false;
        if (!ContainerProvider.TryResolve<IArtisanQuestOwnerContext>(out var context)) return false;
        if (ArtisanPlayerChangedPatch.PreviousPlayer != null &&
            !context.IsOwnedBy(__instance.QuestGiver, ArtisanPlayerChangedPatch.PreviousPlayer)) return false;
        return context.TryEnter(__instance.QuestGiver, out __state);
    }

    [HarmonyFinalizer]
    private static void Finalizer(IDisposable __state) => __state?.Dispose();
}

[HarmonyPatch(typeof(QuestManager), nameof(QuestManager.OnGameLoaded))]
internal class ArtisanOrphanLoadCleanupPatch
{
    [ThreadStatic]
    private static int depth;

    internal static bool IsActive => depth > 0;

    [HarmonyPrefix]
    private static void Prefix() => depth++;

    [HarmonyFinalizer]
    private static void Finalizer() => depth--;
}

[HarmonyPatch(typeof(QuestManager), nameof(QuestManager.OnPlayerCharacterChanged))]
internal class ArtisanPlayerChangedPatch
{
    [ThreadStatic]
    internal static Hero PreviousPlayer;

    [HarmonyPrefix]
    private static void Prefix(Hero oldPlayer, out Hero __state)
    {
        __state = PreviousPlayer;
        PreviousPlayer = oldPlayer;
    }

    [HarmonyFinalizer]
    private static void Finalizer(Hero __state) => PreviousPlayer = __state;
}
