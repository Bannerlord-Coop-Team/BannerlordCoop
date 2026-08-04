using Common;
using Common.Messaging;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Captures + broadcasts the result of a genuine server-side <c>IssueManager.CreateNewIssue</c> creating a
/// <see cref="RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryIssue"/>, so every client replicates the
/// exact same rolled enemy kingdom instead of independently re-rolling
/// <c>Kingdom.All.Where(k =&gt; k.IsAtWarWith(...)).GetRandomElementInefficiently()</c> locally - same shape as
/// <c>TheConquestOfSettlementIssueCreationPatch</c>.
///
/// Deliberately its own, independent postfix-only class rather than a change to
/// <see cref="IssueManagerCreateNewIssuePatches"/> - that type's Prefix (client-creation blocking) is already
/// fully generic, so it already covers this issue type's creation with zero changes; only a second postfix is
/// needed to also capture/broadcast THIS type's result.
/// </summary>
[HarmonyPatch(typeof(IssueManager))]
internal class RaidAnEnemyTerritoryIssueCreationPatch
{
    [HarmonyPatch(nameof(IssueManager.CreateNewIssue))]
    [HarmonyPostfix]
    private static void Postfix(Hero issueOwner, bool __result)
    {
        if (!__result || ModInformation.IsClient) return;
        if (issueOwner?.Issue is not RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryIssue issue) return;

        MessageBroker.Instance.Publish(issueOwner, new RaidAnEnemyTerritoryIssueCreated(issue));
    }
}
