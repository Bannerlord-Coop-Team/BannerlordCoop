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
/// Captures this accept's authoritative <c>_thug</c> roll - see <see cref="IBettingFraudIssueInterface"/>'s doc
/// comment for why this is the only capture Betting Fraud actually needs. Own independent postfix, same shape
/// as <see cref="GangLeaderNeedsRecruitsAcceptancePatches"/>.
///
/// No ownership-gate patch file exists alongside this one - deliberately: see
/// <see cref="IBettingFraudIssueInterface"/>'s doc comment for why this issue type's entire post-accept
/// gameplay loop is already architecturally confined to the real owner's own machine without one.
/// </summary>
[HarmonyPatch(typeof(IssueManager))]
internal class BettingFraudAcceptancePatches
{
    [HarmonyPatch(nameof(IssueManager.StartIssueQuest))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if (!__result) return;
        if (issueOwner?.Issue is not BettingFraudIssueBehavior.BettingFraudIssue) return;

        if (!ContainerProvider.TryResolve<IBettingFraudIssueInterface>(out var issueInterface)) return;
        if (!issueInterface.TryCaptureQuestFields(issueOwner, out var thug)) return;

        ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
        MessageBroker.Instance.Publish(issueOwner,
            new BettingFraudIssueQuestAcceptTriggered(issueOwner, controllerIdProvider?.ControllerId, thug));
    }
}
