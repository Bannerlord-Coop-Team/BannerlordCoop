using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using SandBox.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Defense-in-depth for the save/reload leak <see cref="RivalGangMovingInInstanceResolutionPatch"/>'s own doc
/// comment describes (doc/RivalGangMovingIn_Design_v2.md §3's residual-risk note, closed per §7): following the
/// exact established precedent (<see cref="NearbyBanditBaseOwnershipGatePatches"/> - gate only the REWARD-bearing
/// consequence methods reached by campaign-wide symmetric-event listeners, leave pure-cancel paths alone since a
/// generic cancel is already idempotent/safe via the existing <see cref="IssueFinalizedPatches"/> choke point),
/// of <c>RivalGangMovingInIssueQuest</c>'s 6 <c>RegisterEvents()</c> listeners, 2 reach real
/// reward/penalty-applying consequence methods and are worth gating as cheap insurance against a peer whose own
/// <c>_quests</c> leaked a second, non-owned, but genuinely <c>IsOngoing</c> quest object (see the save/reload
/// scenario above) collecting these penalties a second time:
///
/// - <c>OnPlayerAttackedQuestGiverAlley</c> (reached via <c>OnAlleyClearedByPlayer</c>/<c>OnAlleyOccupiedByPlayer</c>
///   -&gt; <c>OnPlayerAlleyFightEnd</c>): applies a -150 Honor trait change, -10 power, -8 relation, -10 town
///   security, then <c>CompleteQuestWithFail</c>.
/// - <c>OnSettlementOwnerChanged</c>: applies -10 power + -5 relation before cancel.
///
/// <c>OnHeroKilled</c>, <c>OnSiegeEventStarted</c>, <c>OnClanChangedKingdom</c>, and <c>OnWarDeclared</c> (via the
/// shared, ungated <c>QuestHelper.CheckWarDeclarationAndFailOrCancelTheQuest</c>, the same helper several other
/// already-shipped types in this family use unmodified) all reach only <c>CompleteQuestWithCancel</c> with no
/// reward/penalty - left ungated, matching precedent.
/// </summary>
[HarmonyPatch(typeof(RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest))]
internal class RivalGangMovingInOwnershipGatePatches
{
    [HarmonyPatch("OnPlayerAttackedQuestGiverAlley")]
    [HarmonyPrefix]
    private static bool OnPlayerAttackedQuestGiverAlleyPrefix(RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("OnSettlementOwnerChanged")]
    [HarmonyPrefix]
    private static bool OnSettlementOwnerChangedPrefix(RivalGangMovingInIssueBehavior.RivalGangMovingInIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);
}
