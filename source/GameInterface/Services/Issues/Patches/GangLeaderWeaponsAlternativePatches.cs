using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Generic.Migrated.GangLeaderNeedsWeapons;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Patches;

using Issue = GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue;

[HarmonyPatch]
internal static class GangLeaderWeaponsAlternativeDialogPatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.issue_offer_player_accept_alternative_3_consequence));
        yield return AccessTools.Method(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.issue_offer_player_accept_alternative_4_consequence));
        yield return AccessTools.Method(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.issue_offer_player_accept_alternative_5_a_condition));
        yield return AccessTools.Method(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.issue_offer_player_accept_alternative_5_a_consequence));
        yield return AccessTools.Method(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.issue_offer_player_accept_alternative_5_b_consequence));
        yield return AccessTools.Method(typeof(IssuesCampaignBehavior), nameof(IssuesCampaignBehavior.PartyScreenDoneClicked));
    }

    [HarmonyPrefix]
    private static bool Prefix(MethodBase __originalMethod)
    {
        if (ModInformation.IsServer) return true;
        var owner = Hero.OneToOneConversationHero;
        if (owner?.Issue == null) return false;
        if (owner.Issue is not Issue issue) return true;
        if (!issue.IsOngoingWithoutQuest ||
            (ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownership) &&
                ownership.TryGetOwnerControllerId(owner, out _))) return false;
        if (__originalMethod.Name == nameof(IssuesCampaignBehavior.PartyScreenDoneClicked) &&
            issue.AlternativeSolutionSentTroops.TotalHeroes == 0) return false;
        if (__originalMethod.Name != nameof(IssuesCampaignBehavior.issue_offer_player_accept_alternative_5_b_consequence)) return true;
        if (ContainerProvider.TryResolve<IGangLeaderWeaponsAcceptance>(out var acceptance))
            acceptance.RestoreAlternativeSelection(owner, closeScreen: false);
        return false;
    }
}

[HarmonyPatch(typeof(ConversationManager), nameof(ConversationManager.EndConversation))]
internal static class GangLeaderWeaponsSelectionConversationEndPatch
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        if (ModInformation.IsClient &&
            ContainerProvider.TryResolve<IGangLeaderWeaponsAcceptance>(out var acceptance))
            acceptance.RestoreAlternativeSelection(Hero.OneToOneConversationHero, closeScreen: true);
    }
}

[HarmonyPatch(typeof(Issue), nameof(Issue.AlternativeSolutionCondition))]
internal static class GangLeaderWeaponsAlternativeEligibilityPatch
{
    [HarmonyPostfix]
    private static void Postfix(Issue __instance, ref bool __result, ref TextObject explanation)
    {
        if (__result && !CallOriginalPolicy.IsOriginalAllowedForOwnershipGate())
            __result = __instance.CheckPreconditions(__instance.IssueOwner, out explanation);
    }
}

[HarmonyPatch]
internal static class GangLeaderWeaponsAlternativeCompletionPatch
{
    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithAlternativeSolution));
        yield return AccessTools.Method(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithCancel));
    }

    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static bool Prefix(IssueBase __instance, out IDisposable __state)
    {
        __state = null;
        if (__instance is not Issue issue || !issue.IsSolvingWithAlternative ||
            CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;
        return ModInformation.IsServer && issue.IssueOwner.Issue == issue &&
            ContainerProvider.TryResolve<IGangLeaderWeaponsOwnerContext>(out var context) && context.TryOpen(issue, out __state);
    }

    [HarmonyFinalizer]
    private static void Finalizer(IDisposable __state) => __state?.Dispose();
}

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.AddLog))]
internal static class GangLeaderWeaponsAlternativeLogPatch
{
    [HarmonyPostfix]
    private static void Postfix(IssueBase __instance)
    {
        if (ModInformation.IsServer && __instance is Issue issue && issue.IsSolvingWithAlternative)
            MessageBroker.Instance.Publish(issue, new GangLeaderWeaponsAlternativeUpdated(issue));
    }
}

[HarmonyPatch(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.OnIssueUpdated))]
internal static class GangLeaderWeaponsAlternativeOutcomePatch
{
    [HarmonyPostfix]
    private static void Postfix(IssueBase issue, IssueBase.IssueUpdateDetails details)
    {
        if (ModInformation.IsClient || issue is not Issue weapons || !weapons.IsSolvingWithAlternative) return;
        if (details == IssueBase.IssueUpdateDetails.SentTroopsFinishedQuest)
            IssueManagerQuestCompletedReasonCapture.PendingReasons[issue.IssueOwner] = IssueFinalizeReason.AlternativeSolutionSuccess;
        else if (details == IssueBase.IssueUpdateDetails.SentTroopsFailedQuest)
            IssueManagerQuestCompletedReasonCapture.PendingReasons[issue.IssueOwner] = IssueFinalizeReason.AlternativeSolutionFail;
        MessageBroker.Instance.Publish(issue, new GangLeaderWeaponsAlternativeUpdated(weapons, details));
    }
}

[HarmonyPatch(typeof(IssueManager), nameof(IssueManager.DailyTick))]
internal static class GangLeaderWeaponsAlternativeProgressPatch
{
    [HarmonyPostfix]
    private static void Postfix(IssueManager __instance)
    {
        if (ModInformation.IsClient) return;
        foreach (var entry in __instance.Issues)
        {
            if (entry.Value is Issue issue && issue.IsSolvingWithAlternative)
                MessageBroker.Instance.Publish(issue, new GangLeaderWeaponsAlternativeUpdated(issue));
        }
    }
}
