using Common;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Party;
using Helpers;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Generic.Dispatch;

[HarmonyPatch(typeof(IssueManager))]
internal class GenericQuestTypeCreationTriggerPatch
{
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(nameof(IssueManager.CreateNewIssue))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        if (!__result || ModInformation.IsClient) return;

        var issue = issueOwner?.Issue;
        var descriptor = QuestTypeRegistry.Get(issue);
        descriptor?.OnGenuineCreation?.Invoke(issue);
    }
}

[HarmonyPatch(typeof(IssueBase))]
internal class GenericQuestTypeQuestSolutionAcceptTriggerPatch
{
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(nameof(IssueBase.StartIssueWithQuest))]
    [HarmonyPostfix]
    private static void Postfix(IssueBase __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed() || IssueDispatchReplayGuard.IsActive) return;
        if (QuestSolutionStartAuthorityGuard.IsActive) return;

        var issueOwner = __instance.IssueOwner;
        var descriptor = QuestTypeRegistry.Get(__instance);
        if (descriptor?.SupportsQuestSolutionAccept != true) return;

        ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
        descriptor.OnGenuineQuestSolutionAccept?.Invoke(issueOwner, controllerIdProvider?.ControllerId);
        MessageBroker.Instance.Publish(issueOwner, new QuestTypeQuestSolutionAcceptTriggered(issueOwner, controllerIdProvider?.ControllerId));
    }
}

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.StartIssueWithQuest))]
internal class GenericQuestTypeQuestSolutionStartOwnershipGatePatch
{
    [HarmonyPrefix]
    internal static bool Prefix(IssueBase __instance, ref bool __result)
    {
        if (QuestTypeRegistry.Get(__instance)?.SupportsQuestSolutionAccept != true) return true;
        if (QuestSolutionStartAuthorityGuard.IsActive) return true;
        if (CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return true;

        __result = true;
        return false;
    }
}

[HarmonyPatch(typeof(IssueBase))]
internal class GenericQuestTypeAlternativeAcceptTriggerPatch
{
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(nameof(IssueBase.StartIssueWithAlternativeSolution))]
    [HarmonyPostfix]
    private static void Postfix(IssueBase __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed() || IssueDispatchReplayGuard.IsActive) return;
        if (AlternativeSolutionStartAuthorityGuard.IsActive) return;

        var descriptor = QuestTypeRegistry.Get(__instance);
        if (descriptor?.SupportsAlternativeAccept != true) return;

        ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
        descriptor.OnGenuineAlternativeAccept?.Invoke(__instance.IssueOwner, controllerIdProvider?.ControllerId);
        MessageBroker.Instance.Publish(__instance, new QuestTypeAlternativeAcceptTriggered(__instance.IssueOwner, controllerIdProvider?.ControllerId));
    }
}

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithAlternativeSolution))]
internal class GenericQuestTypeAlternativeSolutionOwnershipGatePatch
{
    [HarmonyPrefix]
    internal static bool Prefix(IssueBase __instance)
    {
        if (QuestTypeRegistry.Get(__instance)?.SupportsAlternativeAccept != true) return true;

        return (ContainerProvider.TryResolve<IIssueOwnershipRegistry>(out var ownershipRegistry) && ownershipRegistry.IsLocalPeerOwner(__instance.IssueOwner))
            || AlternativeSolutionCompletionAuthorityGuard.IsActive
            || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate();
    }
}

[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.StartIssueWithAlternativeSolution))]
internal class GenericQuestTypeAlternativeSolutionStartOwnershipGatePatch
{
    [HarmonyPrefix]
    internal static bool Prefix(IssueBase __instance)
    {
        if (QuestTypeRegistry.Get(__instance)?.SupportsAlternativeAccept != true) return true;

        return AlternativeSolutionStartAuthorityGuard.IsActive || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate();
    }
}

[HarmonyPatch(typeof(IssuesCampaignBehavior))]
internal static class GenericQuestTypeAlternativePickerPatch
{
    private static IIssueConversationTracker Tracker =>
        ContainerProvider.TryResolve<IIssueConversationTracker>(out var tracker) ? tracker : null;

    internal static IssueBase CurrentIssue()
    {
        if (ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return null;
        if (Tracker?.AlternativePickerIssue is IssueBase captured) return captured;
        var issue = Hero.OneToOneConversationHero?.Issue;
        return issue?.IsOngoingWithoutQuest == true && QuestTypeRegistry.Get(issue)?.SupportsAlternativeAccept == true
            ? issue : null;
    }

    internal static void BeginSelection()
    {
        Tracker?.TrackAlternativePicker(null, 0);
        var issue = CurrentIssue();
        if (issue != null) CaptureSelection(issue);
    }

    private static void CaptureSelection(IssueBase issue)
    {
        var generation = 0;
        if (ContainerProvider.TryResolve<IIssueGenerationRegistry>(out var generations))
            generations.TryGetGeneration(issue.IssueOwner, out generation);
        Tracker?.TrackAlternativePicker(issue, generation);
    }

    internal static bool IsCurrentSelection(IssueBase issue)
    {
        if (!ReferenceEquals(Hero.OneToOneConversationHero?.Issue, issue) || !issue.IsOngoingWithoutQuest) return false;
        var tracker = Tracker;
        if (tracker?.AlternativePickerIssue != issue) return true;
        var generation = 0;
        if (ContainerProvider.TryResolve<IIssueGenerationRegistry>(out var generations))
            generations.TryGetGeneration(issue.IssueOwner, out generation);
        return generation == tracker.AlternativePickerGeneration;
    }

    private static void CloseStaleSelection()
    {
        // Let DoOption finish before the normal closing transition tears down the conversation.
        var conversation = Campaign.Current.ConversationManager;
        if (conversation.IsConversationInProgress)
            conversation.ActiveToken = conversation.GetStateIndex("close_window");
    }

    [HarmonyPrefix, HarmonyPatch(nameof(IssuesCampaignBehavior.issue_offer_player_accept_alternative_3_consequence))]
    private static bool SelectCompanion()
    {
        var issue = CurrentIssue();
        if (issue == null) return true;
        if (!IsCurrentSelection(issue))
        {
            CloseStaleSelection();
            return false;
        }
        CaptureSelection(issue);
        if (ConversationSentence.SelectedRepeatObject is Hero hero)
        {
            issue.AlternativeSolutionSentTroops.Clear();
            issue.AlternativeSolutionSentTroops.AddToCounts(hero.CharacterObject, 1);
        }
        return false;
    }

    [HarmonyPrefix, HarmonyPatch(nameof(IssuesCampaignBehavior.issue_offer_player_accept_alternative_4_consequence))]
    private static bool OpenPartyScreen()
    {
        var issue = CurrentIssue();
        if (issue == null || IsCurrentSelection(issue)) return true;
        CloseStaleSelection();
        return false;
    }

    [HarmonyPrefix, HarmonyPatch(nameof(IssuesCampaignBehavior.issue_offer_player_accept_alternative_5_b_consequence))]
    private static bool CancelSelection()
    {
        var issue = CurrentIssue();
        if (issue == null) return true;
        if (!IsCurrentSelection(issue))
        {
            CloseStaleSelection();
            return false;
        }
        issue.AlternativeSolutionSentTroops.Clear();
        return false;
    }

    [HarmonyPrefix, HarmonyPatch(nameof(IssuesCampaignBehavior.issue_offer_player_accept_alternative_5_a_consequence))]
    private static bool AcceptSelection()
    {
        var issue = CurrentIssue();
        if (issue == null) return true;
        if (!IsCurrentSelection(issue))
        {
            CloseStaleSelection();
            return false;
        }
        // The server acceptance runs the consequence after validating and removing these troops.
        issue.StartIssueWithAlternativeSolution();
        return false;
    }

    [HarmonyPrefix, HarmonyPatch(nameof(IssuesCampaignBehavior.PartyScreenDoneClicked))]
    private static bool RetainSelection(TroopRoster leftMemberRoster)
    {
        var issue = CurrentIssue();
        if (issue == null) return true;
        if (!IsCurrentSelection(issue))
        {
            CloseStaleSelection();
            return Campaign.Current.ConversationManager.IsConversationInProgress;
        }
        if (!ReferenceEquals(issue.AlternativeSolutionSentTroops, leftMemberRoster))
        {
            issue.AlternativeSolutionSentTroops.Clear();
            issue.AlternativeSolutionSentTroops.Add(leftMemberRoster);
        }
        return true;
    }

    [HarmonyPrefix, HarmonyPatch(nameof(IssuesCampaignBehavior.issue_offer_player_accept_alternative_3_condition))]
    private static bool CanListCompanion(ref bool __result) => CheckSelection(ref __result);

    [HarmonyPrefix, HarmonyPatch(nameof(IssuesCampaignBehavior.issue_offer_player_accept_alternative_5_a_condition))]
    private static bool CanAccept(ref bool __result) => CheckSelection(ref __result);

    [HarmonyPrefix, HarmonyPatch(nameof(IssuesCampaignBehavior.TroopTransferableDelegate))]
    private static bool CanTransfer(ref bool __result) => CheckSelection(ref __result);

    private static bool CheckSelection(ref bool result)
    {
        var issue = CurrentIssue();
        if (issue == null || IsCurrentSelection(issue)) return true;
        result = false;
        return false;
    }

    [HarmonyPrefix, HarmonyPatch(nameof(IssuesCampaignBehavior.DoTroopsSatisfyAlternativeSolutionInternal))]
    private static bool CheckTroops(ref bool __result, ref TextObject explanation)
    {
        if (CheckSelection(ref __result)) return true;
        explanation = new TextObject("{=coop_issue_no_longer_available}This task is no longer available.");
        return false;
    }

    internal static TroopRoster CombineRosters(TroopRoster right, TroopRoster left)
    {
        using (new AllowedThread())
        {
            var combined = TroopRoster.CreateDummyTroopRoster();
            combined.Add(right);
            foreach (var element in left.GetTroopRoster())
            {
                if (element.Character.IsHero && combined.GetTroopCount(element.Character) > 0) continue;
                combined.AddToCounts(element.Character, element.Number, false, element.WoundedNumber, element.Xp);
            }
            return combined;
        }
    }
}

[HarmonyPatch(typeof(PartyScreenLogic))]
internal static class GenericQuestTypeAlternativePickerResetPatch
{
    [HarmonyPrefix, HarmonyPatch(nameof(PartyScreenLogic.ResetLogic))]
    private static void Reset(PartyScreenLogic __instance) => DetachStaleRoster(__instance);

    [HarmonyPrefix, HarmonyPatch(nameof(PartyScreenLogic.ResetToLastSavedPartyScreenData))]
    private static void ResetSaved(PartyScreenLogic __instance) => DetachStaleRoster(__instance);

    private static void DetachStaleRoster(PartyScreenLogic logic)
    {
        if (ModInformation.IsServer || logic._partyScreenMode != PartyScreenHelper.PartyScreenMode.QuestTroopManage
            || !ContainerProvider.TryResolve<IIssueConversationTracker>(out var tracker)) return;
        var issue = tracker.AlternativePickerIssue;
        if (issue == null || GenericQuestTypeAlternativePickerPatch.IsCurrentSelection(issue)
            || !ReferenceEquals(logic.CurrentData.LeftMemberRoster, issue.AlternativeSolutionSentTroops)) return;

        // A stale screen must not restore its draft over another player's accepted mission.
        using (new AllowedThread())
        {
            var draft = TroopRoster.CreateDummyTroopRoster();
            logic.CurrentData.LeftMemberRoster = draft;
            logic.MemberRosters[(int)PartyScreenLogic.PartyRosterSide.Left] = draft;
        }
    }
}
