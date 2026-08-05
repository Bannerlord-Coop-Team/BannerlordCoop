using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Finding 1 fix: the vanilla "accept the quest solution" transition ran purely locally on whichever
/// machine's own conversation triggered it, with nothing captured or broadcast (see the decompiled
/// <c>IssuesCampaignBehavior.AddDialogues</c>'s <c>issue_offer_player_accept_quest</c> consequence, wired
/// straight to <see cref="IssueManager.StartIssueQuest"/>). Not blocked here - see the doc comment on the
/// postfix below for why - but every genuine (non-replay) transition is now routed through the server so
/// every OTHER peer's own copy of the issue is kept in sync, and a losing peer in a same-issue
/// double-accept race is told to roll its own local copy back. See
/// <see cref="Handlers.VillageNeedsToolsIssueHandler"/> for the request/broadcast/reject routing, and
/// <see cref="Interfaces.VillageNeedsToolsIssueInterface"/> for the mirror-side replay methods. See
/// <see cref="IssueWithAlternativeSolutionAcceptancePatch"/> below for the alternative-solution equivalent.
/// </summary>
[HarmonyPatch(typeof(IssueManager))]
internal class IssueQuestAcceptancePatch
{
    // Deliberately never blocked (unlike IssueManagerCreateNewIssuePatches' creation gate): this runs inline
    // inside a live one-to-one conversation (the issue_offer_player_accept_quest dialogue consequence), and
    // the very next dialogue line ("issue_classic_quest_start") only exists at all because building the
    // VillageNeedsToolsIssueQuest here registers it with the ConversationManager. Blocking this on a client
    // would leave that dialogue node missing and break/crash their conversation mid-flow, which no reasonable
    // network round-trip can fix without deep, out-of-scope changes to how this engine's dialogue system
    // works. Both the genuine local accept AND a later mirror replay (run under AllowedThread - see
    // VillageNeedsToolsIssueInterface.MirrorQuestAccepted) call this same real method, and both need it to
    // run so every peer derives a byte-identical VillageNeedsToolsIssueQuest - see the type doc comment on
    // VillageNeedsToolsIssueInterface for why that's safe/deterministic here without capturing/broadcasting
    // the quest's own fields separately.
    [HarmonyPatch(nameof(IssueManager.StartIssueQuest))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        // A mirror replay (server authoritative replay, or another peer applying a received broadcast) runs
        // under AllowedThread - skip re-publishing so it doesn't loop back through the network again.
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if (!__result) return;
        // Widened (was hardcoded to VillageNeedsToolsIssue only) to every issue type whose Quest fields are
        // all already frozen at creation time - see GenericAcceptMirrorIssueTypes's doc comment. Types that
        // need their own accept-time force-write (VillageNeedsCraftingMaterialsIssue and others) capture this
        // same moment via their own independent postfix on this same method instead (Harmony runs multiple
        // postfixes on one method without conflict) - see e.g. VillageNeedsCraftingMaterialsQuestAcceptancePatch.
        if (!GenericAcceptMirrorIssueTypes.IsQuestSolutionMirrorEligible(issueOwner?.Issue)) return;

        // Real ownership fix: capture this accepting machine's own ControllerId at the moment of the
        // genuine accept - see VillageNeedsToolsIssueOwnership.
        ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
        MessageBroker.Instance.Publish(issueOwner, new VillageIssueQuestAcceptTriggered(issueOwner, controllerIdProvider?.ControllerId));
    }
}

/// <summary>
/// Same shape/reasoning as <see cref="IssueQuestAcceptancePatch"/>, for
/// <see cref="IssueBase.StartIssueWithAlternativeSolution"/> (wired to the
/// <c>issue_offer_player_accept_alternative_5_a</c> consequence). Only <c>VillageNeedsToolsIssue</c> can
/// ever reach here at all (see <see cref="DisableAllIssueBehaviorsExceptAllowlist"/>), so no other
/// <see cref="IssueBase"/> subtype needs handling.
///
/// Deliberately NOT fully replayed on other peers the way StartIssueQuest is - see
/// <see cref="Interfaces.VillageNeedsToolsIssueInterface.MirrorAlternativeAccepted"/> for why
/// (<c>AlternativeSolutionSentTroops</c>/<c>AlternativeSolutionHero</c> are genuinely local to whichever
/// player ran the party-screen troop-selection flow, and replaying the real method on a peer with an empty
/// troop roster would null-ref).
/// </summary>
[HarmonyPatch(typeof(IssueBase))]
internal class IssueWithAlternativeSolutionAcceptancePatch
{
    [HarmonyPatch(nameof(IssueBase.StartIssueWithAlternativeSolution))]
    [HarmonyPostfix]
    private static void Postfix(IssueBase __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        // Widened (was hardcoded to VillageNeedsToolsIssue only) - see GenericAcceptMirrorIssueTypes's doc
        // comment for which types this covers and why.
        if (!GenericAcceptMirrorIssueTypes.IsAlternativeSolutionMirrorEligible(__instance)) return;

        ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
        MessageBroker.Instance.Publish(__instance, new VillageIssueAlternativeAcceptTriggered(__instance.IssueOwner, controllerIdProvider?.ControllerId));
    }
}
