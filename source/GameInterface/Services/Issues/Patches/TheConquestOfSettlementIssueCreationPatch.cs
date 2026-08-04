using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Captures + broadcasts the result of a genuine server-side <c>IssueManager.CreateNewIssue</c> creating a
/// <see cref="TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue"/>, so every client replicates
/// the exact same picked target settlement instead of independently re-rolling
/// <c>TheConquestOfSettlementIssueBehavior.ConditionsHold</c>'s <c>mBList.GetRandomElement()</c> locally - same
/// shape as <c>CaravanAmbushIssueCreationPatch</c>.
///
/// Deliberately its own, independent postfix-only class rather than a change to
/// <see cref="IssueManagerCreateNewIssuePatches"/> - that type's Prefix (client-creation blocking) is already
/// fully generic, so it already covers this issue type's creation with zero changes; only a second postfix is
/// needed to also capture/broadcast THIS type's result.
/// </summary>
[HarmonyPatch(typeof(IssueManager))]
internal class TheConquestOfSettlementIssueCreationPatch
{
    [HarmonyPatch(nameof(IssueManager.CreateNewIssue))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        if (!__result || ModInformation.IsClient) return;
        if (issueOwner?.Issue is not TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue issue) return;

        MessageBroker.Instance.Publish(issueOwner, new TheConquestOfSettlementIssueCreated(issue));
    }
}
