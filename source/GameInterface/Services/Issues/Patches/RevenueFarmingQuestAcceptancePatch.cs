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
/// Same shape/reasoning as <see cref="VillageNeedsCraftingMaterialsQuestAcceptancePatch"/> (see that type's doc
/// comment), scoped to <see cref="RevenueFarmingIssueBehavior.RevenueFarmingIssue"/>. Deliberately its own,
/// independent postfix-only class rather than a change to <see cref="IssueAcceptancePatches"/> - Harmony runs
/// multiple independent postfixes on the same vanilla method without conflict, and this issue type is NOT in
/// <see cref="GenericAcceptMirrorIssueTypes.QuestSolutionMirrorEligible"/> at all (see
/// <see cref="Interfaces.IRevenueFarmingIssueInterface"/>'s type doc comment for why), so
/// <see cref="IssueQuestAcceptancePatch"/>'s own postfix always skips this issue type's accept - this is the
/// ONLY postfix on <c>IssueManager.StartIssueQuest</c> that ever fires for it.
///
/// Additionally captures this accept's authoritative <c>_totalRequestedDenars</c>/<c>_revenueVillages</c> - the
/// central mechanism this issue type needs (see <see cref="Interfaces.IRevenueFarmingIssueInterface"/>'s type
/// doc comment): both are re-derived per-client at accept time, so whichever machine's accept is genuine must
/// capture what it just baked, right here, for the server to arbitrate and broadcast. This type has no
/// alternative solution, so there is no alternative-solution-accept equivalent patch here at all.
/// </summary>
[HarmonyPatch(typeof(IssueManager))]
internal class RevenueFarmingQuestAcceptancePatch
{
    [HarmonyPatch(nameof(IssueManager.StartIssueQuest))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if (!__result) return;
        if (issueOwner?.Issue is not RevenueFarmingIssueBehavior.RevenueFarmingIssue) return;

        if (!ContainerProvider.TryResolve<IRevenueFarmingIssueInterface>(out var issueInterface)) return;
        if (!issueInterface.TryCaptureQuestFields(issueOwner, out var totalRequestedDenars, out var villages)) return;

        ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
        MessageBroker.Instance.Publish(issueOwner,
            new RevenueFarmingQuestAcceptTriggered(issueOwner, controllerIdProvider?.ControllerId, totalRequestedDenars, villages));
    }
}
