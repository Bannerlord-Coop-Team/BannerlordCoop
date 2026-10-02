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
    internal static IssueBase CurrentIssue()
    {
        if (ModInformation.IsServer || CallOriginalPolicy.IsOriginalAllowedForOwnershipGate()) return null;
        var issue = Hero.OneToOneConversationHero?.Issue;
        return issue?.IsOngoingWithoutQuest == true && QuestTypeRegistry.Get(issue)?.SupportsAlternativeAccept == true
            ? issue : null;
    }

    [HarmonyPrefix, HarmonyPatch(nameof(IssuesCampaignBehavior.issue_offer_player_accept_alternative_3_consequence))]
    private static bool SelectCompanion()
    {
        var issue = CurrentIssue();
        if (issue == null) return true;
        if (ConversationSentence.SelectedRepeatObject is Hero hero)
        {
            issue.AlternativeSolutionSentTroops.Clear();
            issue.AlternativeSolutionSentTroops.AddToCounts(hero.CharacterObject, 1);
        }
        return false;
    }

    [HarmonyPrefix, HarmonyPatch(nameof(IssuesCampaignBehavior.issue_offer_player_accept_alternative_5_b_consequence))]
    private static bool CancelSelection()
    {
        var issue = CurrentIssue();
        if (issue == null) return true;
        issue.AlternativeSolutionSentTroops.Clear();
        return false;
    }

    [HarmonyPrefix, HarmonyPatch(nameof(IssuesCampaignBehavior.issue_offer_player_accept_alternative_5_a_consequence))]
    private static bool AcceptSelection()
    {
        var issue = CurrentIssue();
        if (issue == null) return true;
        // The server acceptance runs the consequence after validating and removing these troops.
        issue.StartIssueWithAlternativeSolution();
        return false;
    }

    [HarmonyPrefix, HarmonyPatch(nameof(IssuesCampaignBehavior.PartyScreenDoneClicked))]
    private static void RetainSelection(TroopRoster leftMemberRoster)
    {
        var issue = CurrentIssue();
        if (issue == null || ReferenceEquals(issue.AlternativeSolutionSentTroops, leftMemberRoster)) return;
        issue.AlternativeSolutionSentTroops.Clear();
        issue.AlternativeSolutionSentTroops.Add(leftMemberRoster);
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
