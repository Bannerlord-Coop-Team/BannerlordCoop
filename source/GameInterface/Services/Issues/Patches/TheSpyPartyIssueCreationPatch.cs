using Common;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Entity;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using SandBox.Issues;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Captures + broadcasts a genuine server-side creation of a
/// <see cref="TheSpyPartyIssueQuestBehavior.TheSpyPartyIssue"/> - see <see cref="ITheSpyPartyIssueInterface"/>'s
/// doc comment. Own independent postfix, same reasoning as <see cref="VillageNeedsCraftingMaterialsIssueCreationPatch"/>.
/// </summary>
[HarmonyPatch(typeof(IssueManager))]
internal class TheSpyPartyIssueCreationPatch
{
    [HarmonyPatch(nameof(IssueManager.CreateNewIssue))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        if (!__result || ModInformation.IsClient) return;
        if (issueOwner?.Issue is not TheSpyPartyIssueQuestBehavior.TheSpyPartyIssue spyIssue) return;

        MessageBroker.Instance.Publish(issueOwner, new TheSpyPartyIssueCreated(spyIssue));
    }
}

/// <summary>
/// Captures this accept's authoritative selected-spy identity - see
/// <see cref="ITheSpyPartyIssueInterface"/>'s type doc comment for why this type needs its own bespoke
/// accept-time capture instead of riding the fully generic mirror. Own independent postfix on the same
/// <see cref="IssueManager.StartIssueQuest"/> method the generic <see cref="IssueQuestAcceptancePatch"/> also
/// patches - Harmony runs multiple postfixes on one method without conflict (same shape as
/// <see cref="VillageNeedsCraftingMaterialsAcceptancePatches"/>); the generic one no-ops for this type since it
/// isn't in <see cref="GenericAcceptMirrorIssueTypes.QuestSolutionMirrorEligible"/>.
/// </summary>
[HarmonyPatch(typeof(IssueManager))]
internal class TheSpyPartyIssueAcceptancePatch
{
    [HarmonyPatch(nameof(IssueManager.StartIssueQuest))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if (!__result) return;
        if (issueOwner?.Issue is not TheSpyPartyIssueQuestBehavior.TheSpyPartyIssue) return;

        ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
        MessageBroker.Instance.Publish(issueOwner,
            new TheSpyPartyIssueQuestAcceptTriggered(issueOwner, controllerIdProvider?.ControllerId));
    }
}

/// <summary>
/// Bug-1-shaped fix (see <see cref="VillageNeedsToolsQuestOwnershipGatePatch"/>'s doc comment for the full
/// derivation): <c>TheSpyPartyIssueQuest</c>'s resolution is a scripted arena duel
/// (<c>CampaignMission.OpenArenaDuelMission</c>) reachable by ANY peer who tracks down and challenges a suspect
/// in the shared/mirrored settlement, exactly the same "anyone can turn in someone else's accepted quest" shape
/// as every other type here. Gates all four reward-applying outcome methods
/// (<c>PlayerFoundTheSpyAndKilledHim</c>/<c>PlayerCouldNotFoundTheSpyAndKilledAnotherSuspect</c>/
/// <c>PlayerFoundTheSpyButLostTheFight</c>/<c>PlayerCouldNotFoundTheSpyAndLostTheFight</c>) to the recorded owner
/// only. <c>OnTimedOut</c> (ambient, symmetric across peers) is deliberately NOT gated, matching precedent.
/// </summary>
[HarmonyPatch(typeof(TheSpyPartyIssueQuestBehavior.TheSpyPartyIssueQuest))]
internal class TheSpyPartyOwnershipGatePatches
{
    [HarmonyPatch("PlayerFoundTheSpyAndKilledHim")]
    [HarmonyPrefix]
    private static bool PlayerFoundTheSpyAndKilledHimPrefix(TheSpyPartyIssueQuestBehavior.TheSpyPartyIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("PlayerCouldNotFoundTheSpyAndKilledAnotherSuspect")]
    [HarmonyPrefix]
    private static bool PlayerCouldNotFoundTheSpyAndKilledAnotherSuspectPrefix(TheSpyPartyIssueQuestBehavior.TheSpyPartyIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("PlayerFoundTheSpyButLostTheFight")]
    [HarmonyPrefix]
    private static bool PlayerFoundTheSpyButLostTheFightPrefix(TheSpyPartyIssueQuestBehavior.TheSpyPartyIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("PlayerCouldNotFoundTheSpyAndLostTheFight")]
    [HarmonyPrefix]
    private static bool PlayerCouldNotFoundTheSpyAndLostTheFightPrefix(TheSpyPartyIssueQuestBehavior.TheSpyPartyIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);
}
