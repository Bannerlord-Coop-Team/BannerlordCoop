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
/// Captures this accept's authoritative <c>_numberOfDaggersRequested</c> - see
/// <see cref="IGangLeaderNeedsSpecialWeaponsIssueInterface"/>'s doc comment. Own independent postfix, same
/// shape as <see cref="GangLeaderNeedsRecruitsAcceptancePatches"/>.
///
/// No ownership-gate patch file exists alongside this one - deliberately: see
/// <see cref="IGangLeaderNeedsSpecialWeaponsIssueInterface"/>'s doc comment for why both the survey's flagged
/// per-crafting-order roll AND the turn-in check are already self-confined to the real owner's own machine.
/// </summary>
[HarmonyPatch(typeof(IssueManager))]
internal class GangLeaderNeedsSpecialWeaponsAcceptancePatches
{
    [HarmonyPatch(nameof(IssueManager.StartIssueQuest))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
        if (!__result) return;
        if (issueOwner?.Issue is not GangLeaderNeedsSpecialWeaponsIssueBehavior.GangLeaderNeedsSpecialWeaponsIssue) return;

        if (!ContainerProvider.TryResolve<IGangLeaderNeedsSpecialWeaponsIssueInterface>(out var issueInterface)) return;
        if (!issueInterface.TryCaptureQuestFields(issueOwner, out var numberOfDaggersRequested)) return;

        ContainerProvider.TryResolve<IControllerIdProvider>(out var controllerIdProvider);
        MessageBroker.Instance.Publish(issueOwner,
            new GangSpecialWeaponsIssueQuestAcceptTriggered(issueOwner, controllerIdProvider?.ControllerId, numberOfDaggersRequested));
    }
}
