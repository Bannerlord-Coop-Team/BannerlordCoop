using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Bug-1-shaped fix (see <see cref="VillageNeedsToolsQuestOwnershipGatePatch"/>'s doc comment for the full
/// derivation): <c>LandLordTheArtOfTheTradeIssueQuest</c>'s initial "how do you want to pay this back"
/// three-option offer (<c>OfferDialogFlow</c>) is safe unguarded - it only ever runs inline in the SAME live
/// conversation that just accepted the quest (a mirrored replay never opens a local conversation), exactly like
/// every other type's initial accept dialogue. The exploitable turn-in instead lives entirely in the LATER,
/// separate <c>DiscussDialogFlow</c> ("quest_discuss") conversation, reachable by ANY peer who can walk up to
/// the (shared, mirrored) quest giver - its own gating condition (<c>QuestCanBeFinalized</c>) only checks
/// <c>Hero.OneToOneConversationHero == QuestGiver</c> plus this machine's OWN locally-tracked
/// <c>_soldCount</c>/<c>_gatheredDenars</c> (kept symmetric across peers via the shared
/// <c>PlayerInventoryExchangeEvent</c>), with no ownership concept at all. Gates the three real turn-in
/// consequence methods directly (same shape as <see cref="CapturedByBountyHuntersOwnershipGatePatches"/>) rather
/// than the dialogue option's own <c>Condition</c>, since that's the simplest single choke point covering every
/// player-option branch (paid-in-full success, under-sold-but-pays success, and the "refuse to pay" failure).
/// </summary>
[HarmonyPatch(typeof(LandLordTheArtOfTheTradeIssueBehavior.LandLordTheArtOfTheTradeIssueQuest))]
internal class LandLordTheArtOfTheTradeOwnershipGatePatches
{
    [HarmonyPatch("QuestSuccessPlayerSoldTheProducts")]
    [HarmonyPrefix]
    private static bool QuestSuccessPlayerSoldTheProductsPrefix(
        LandLordTheArtOfTheTradeIssueBehavior.LandLordTheArtOfTheTradeIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("PlayerPaidAgreedDenarsQuestSuccess")]
    [HarmonyPrefix]
    private static bool PlayerPaidAgreedDenarsQuestSuccessPrefix(
        LandLordTheArtOfTheTradeIssueBehavior.LandLordTheArtOfTheTradeIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("QuestFailedPlayerBrokeTheAgreement")]
    [HarmonyPrefix]
    private static bool QuestFailedPlayerBrokeTheAgreementPrefix(
        LandLordTheArtOfTheTradeIssueBehavior.LandLordTheArtOfTheTradeIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    // OnTimedOut applies the same ChangeCrimeRatingAction(+5, faction-wide) and a -10 relation hit as
    // QuestFailedPlayerBrokeTheAgreement, but unlike the three methods above it's driven by
    // QuestManager.HourlyTick -> CompleteQuestWithTimeOut(), which is not host/client-gated anywhere in this
    // codebase - every peer's own mirrored quest independently reaches its due time at the same in-game
    // moment and would otherwise apply this penalty once per connected peer instead of once, total.
    [HarmonyPatch("OnTimedOut")]
    [HarmonyPrefix]
    private static bool OnTimedOutPrefix(
        LandLordTheArtOfTheTradeIssueBehavior.LandLordTheArtOfTheTradeIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);
}
