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
/// Routes a genuine local accept through the server so every other peer's own copy of the issue is kept in
/// sync, and rolls back the loser of a same-issue double-accept race. See
/// <see cref="Handlers.VillageNeedsToolsIssueHandler"/> for the request/broadcast/reject routing, and
/// <see cref="Interfaces.VillageNeedsToolsIssueInterface"/> for the mirror-side replay methods.
/// </summary>
[HarmonyPatch(typeof(IssueManager))]
internal class IssueQuestAcceptancePatch
{
    // Deliberately never blocked (unlike IssueManagerCreateNewIssuePatches' creation gate): this runs inline
    // inside a live conversation, and the next dialogue line only exists because building the Quest here
    // registers it with the ConversationManager - blocking it on a client would break their conversation
    // mid-flow. Only the genuine local accept ever reaches this method for real; a mirror replay on every
    // other peer force-writes state instead (see VillageNeedsToolsIssueInterface.MirrorQuestAccepted).
    [HarmonyPatch(nameof(IssueManager.StartIssueQuest))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        // A mirror replay runs under AllowedThread - skip re-publishing so it doesn't loop back through the network.
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if (!__result) return;
        if (!GenericAcceptMirrorIssueTypes.IsQuestSolutionMirrorEligible(issueOwner?.Issue)) return;

        ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
        MessageBroker.Instance.Publish(issueOwner, new VillageIssueQuestAcceptTriggered(issueOwner, controllerIdProvider?.ControllerId));
    }
}

/// <summary>
/// Same shape as <see cref="IssueQuestAcceptancePatch"/>, for <see cref="IssueBase.StartIssueWithAlternativeSolution"/>.
/// Deliberately NOT fully replayed on other peers - see
/// <see cref="Interfaces.VillageNeedsToolsIssueInterface.MirrorAlternativeAccepted"/> for why
/// (<c>AlternativeSolutionSentTroops</c>/<c>AlternativeSolutionHero</c> are local to whichever player ran the
/// party-screen troop-selection flow; replaying the real method on a peer with an empty roster would NRE).
/// </summary>
[HarmonyPatch(typeof(IssueBase))]
internal class IssueWithAlternativeSolutionAcceptancePatch
{
    [HarmonyPatch(nameof(IssueBase.StartIssueWithAlternativeSolution))]
    [HarmonyPostfix]
    private static void Postfix(IssueBase __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if (!GenericAcceptMirrorIssueTypes.IsAlternativeSolutionMirrorEligible(__instance)) return;

        ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
        MessageBroker.Instance.Publish(__instance, new VillageIssueAlternativeAcceptTriggered(__instance.IssueOwner, controllerIdProvider?.ControllerId));
    }
}
