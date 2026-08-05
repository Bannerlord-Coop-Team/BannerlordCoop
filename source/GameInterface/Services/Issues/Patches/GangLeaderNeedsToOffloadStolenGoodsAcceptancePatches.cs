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
/// Same shape/reasoning as <see cref="IssueQuestAcceptancePatch"/> (neither this nor the alternative-solution
/// equivalent below is ever blocked - both run inline inside a live one-to-one conversation), scoped to
/// <see cref="GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue"/>.
/// Deliberately its own, independent postfix-only class rather than a change to
/// <see cref="IssueAcceptancePatches"/> - Harmony runs multiple independent postfixes on the same vanilla
/// method without conflict.
///
/// Additionally captures this accept's authoritative price/reward terms - the central mechanism this issue type
/// needs (see <see cref="IGangLeaderNeedsToOffloadStolenGoodsIssueInterface"/>'s type doc comment): unlike the
/// fully generic mirror-eligible types, these are re-derived per-client at accept time from BOTH live
/// <c>Town.GetItemPrice</c> AND <c>IssueDifficultyMultiplier</c>, so whichever machine's accept is genuine must
/// capture what it just baked, right here, for the server to arbitrate and broadcast.
/// </summary>
[HarmonyPatch(typeof(IssueManager))]
internal class GangLeaderNeedsToOffloadStolenGoodsQuestAcceptancePatch
{
    [HarmonyPatch(nameof(IssueManager.StartIssueQuest))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if (!__result) return;
        if (issueOwner?.Issue is not GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue) return;

        if (!ContainerProvider.TryResolve<IGangLeaderNeedsToOffloadStolenGoodsIssueInterface>(out var issueInterface)) return;
        if (!issueInterface.TryCaptureQuestFields(issueOwner, out var stolenTradeGoodAmount, out var stolenTradeGoodPrice, out var rewardGold, out var counterOfferGold)) return;

        ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
        MessageBroker.Instance.Publish(issueOwner,
            new GangLeaderStolenGoodsQuestAcceptTriggered(
                issueOwner, controllerIdProvider?.ControllerId, stolenTradeGoodAmount, stolenTradeGoodPrice, rewardGold, counterOfferGold));
    }
}

/// <summary>
/// Same shape/reasoning as <see cref="IssueWithAlternativeSolutionAcceptancePatch"/>, scoped to
/// <see cref="GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue"/>.
/// Additionally captures the Issue-level <c>StolenTradeGoodAmount</c>/<c>RewardGold</c> this accept's own
/// <c>StartIssueWithAlternativeSolution</c> call just computed - the values to freeze against later
/// <c>Town.GetItemPrice</c> drift (the task's second flagged gap - see the interface's type doc comment).
/// </summary>
[HarmonyPatch(typeof(IssueBase))]
internal class GangLeaderNeedsToOffloadStolenGoodsAlternativeAcceptancePatch
{
    [HarmonyPatch(nameof(IssueBase.StartIssueWithAlternativeSolution))]
    [HarmonyPostfix]
    private static void Postfix(IssueBase __instance)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if (__instance is not GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue issue) return;

        if (!ContainerProvider.TryResolve<IGangLeaderNeedsToOffloadStolenGoodsIssueInterface>(out var issueInterface)) return;
        if (!issueInterface.TryCaptureAlternativeSolutionFields(issue.IssueOwner, out var stolenTradeGoodAmount, out var rewardGold)) return;

        ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
        MessageBroker.Instance.Publish(issue,
            new GangLeaderStolenGoodsAlternativeAcceptTriggered(issue.IssueOwner, controllerIdProvider?.ControllerId, stolenTradeGoodAmount, rewardGold));
    }
}
