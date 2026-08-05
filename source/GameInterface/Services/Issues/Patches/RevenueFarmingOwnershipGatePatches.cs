using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Bug-1-shaped fix (see <see cref="VillageNeedsToolsQuestOwnershipGatePatch"/>'s doc comment for the full
/// derivation) for <see cref="RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest"/>'s turn-in path and every
/// ambient <c>RegisterEvents</c> listener - independently traced against the decompiled source method-by-method
/// rather than trusted, per this session's established pattern (this quest's own dialogue/menu system was
/// checked for the same two gap shapes found on other quests this session: (a) no reachable party/component
/// spawn exists anywhere in this behavior at all - confirmed by grepping the whole decompiled source for
/// party-creation APIs, zero hits; (b) the turn-in ownership gap below).
///
/// TURN-IN: unlike Artisan Overpriced Goods, this type has NO chained-external-Consequence gap - both real
/// turn-in surfaces call the two gated leaf methods DIRECTLY, inline, from within their own method body:
/// <c>DiscussDialogFlow</c>'s "Yes, here is your share."/"Maybe I should keep this to myself." options each
/// have a single inline <c>.Consequence(delegate { QuestCompletedWithSuccess(); ... })</c>/
/// <c>QuestCompletedWithBetray()</c> that IS the leaf call, not a separately-bound node further down the same
/// dialogue chain. The SECOND turn-in surface, the "Hand over the revenue" game-menu option
/// (<c>talk_to_steward_on_consequence</c>, registered on the BEHAVIOR - not the Quest - via
/// <c>OnAfterSessionLaunchedEvent</c>, with a condition that checks only <c>Settlement.CurrentSettlement.OwnerClan
/// == Instance.QuestGiver.Clan</c>, no ownership concept at all), calls the public
/// <c>RevenuesAreDeliveredToSteward()</c>, which itself calls <c>QuestCompletedWithSuccess()</c> directly - so
/// gating the two leaf methods below covers BOTH turn-in surfaces without needing to touch the behavior-level
/// menu option at all.
///
/// REGISTEREVENTS LISTENERS: traced each of the 7 listeners individually against the decompiled source (and,
/// for the two that delegate to it, <c>Helpers.QuestHelper</c>) rather than assuming the whole set needs a gate
/// wholesale - re-verifying the "RegisterEvents-gated listeners can turn out to be provably inert" possibility
/// this session already confirmed once for a different quest (Family Feud), the same rigor applied here:
///
/// - <c>OnWarDeclared</c> -&gt; <c>QuestHelper.CheckWarDeclarationAndFailOrCancelTheQuest</c>: checks
///   <c>QuestGiver.MapFaction.IsAtWarWith(Hero.MainHero.MapFaction)</c> - <c>Hero.MainHero</c> is THIS PROCESS's
///   own controlled hero, genuinely per-peer-divergent (a war affecting one peer's own clan says nothing about
///   whether the ACTUAL quest owner is at war) - and unconditionally calls
///   <c>CompleteQuestWithFail</c>/<c>CompleteQuestWithCancel</c> when it matches. LOAD-BEARING - gated.
/// - <c>OnClanChangedKingdom</c>: same <c>Hero.MainHero</c>-relative condition, same
///   <c>CompleteQuestWithCancel</c> call. LOAD-BEARING - gated.
/// - <c>OnVillageRaid</c>: mutates <c>_totalRequestedDenars</c>/the affected <c>RevenueVillage</c>'s own
///   <c>SetDone</c>/<c>IsRaided</c>/journal progress, AND (once every village is raided) calls
///   <c>CompleteQuestWithCancel</c> - a real network-visible completion path independently reachable on every
///   peer holding a mirrored copy, the same "every peer reaches IssueFinalized independently" shape
///   <c>GangLeaderNeedsToOffloadStolenGoodsOwnershipGatePatches</c>' doc comment documents for
///   <c>FailQuestByLosingHideoutBattle</c>. LOAD-BEARING - gated (also keeps this quest type's own bespoke
///   <c>_totalRequestedDenars</c>/<c>_revenueVillages</c> sync single-source-of-truth on the owner, rather than
///   relying on every peer's raid-timing landing byte-identically).
/// - <c>OnMapEventStarted</c> -&gt; <c>QuestHelper.CheckMinorMajorCoercion</c>: checks
///   <c>attackerParty == PartyBase.MainParty</c> - genuinely per-peer (only true for whichever peer's own party
///   is the attacker) - and, when true, <c>ApplyGenericMinorMajorCoercionConsequences</c> unconditionally calls
///   <c>CompleteQuestWithFail</c> plus a direct relation/power/trait mutation on the shared QuestGiver. LOAD-
///   BEARING - gated.
/// - <c>OnSettlementOwnerChanged</c>: condition (<c>settlement == TargetSettlement &amp;&amp; oldOwner ==
///   QuestGiver</c>) has no <c>Hero.MainHero</c> dependency and evaluates identically on every peer holding
///   already-synced settlement-ownership state, but still independently calls <c>CompleteQuestWithCancel</c> on
///   every peer that reaches it - the same "correctly triggers redundantly rather than wrongly" shape as
///   <c>OnSettlementLeft</c> in the Gang Leader precedent above, still gated there for the same reason: letting
///   every peer's own genuine (non-replayed) completion call independently cascade into
///   <c>IssueFinalized</c>/the network broadcast is a real N-way-redundant-broadcast risk, not just a benign
///   duplicate read. LOAD-BEARING - gated.
/// - <c>OnMapEventEnded</c>: ONLY ever flips <c>CollectingRevenues = false</c> (no completion call at all) -
///   and <c>CollectingRevenues</c> is read exclusively inside <c>ProgressRevenueCollectionForVillage</c>, itself
///   only ever called from this Quest's own <c>HourlyTick</c> override, which is ALREADY gated below. A non-
///   owner's own local flip of this field is therefore provably inert - it can never be observed by anything,
///   since the one place that reads it never runs on a non-owner mirror. NOT load-bearing - gated anyway below
///   purely as free, zero-risk defense-in-depth (task explicitly permits this), not because it fixes a real gap.
/// - <c>OnPlayerDeserted</c>: same shape/reasoning as <c>OnMapEventEnded</c> above (also only ever flips
///   <c>CollectingRevenues = false</c>). NOT load-bearing - gated anyway as free defense-in-depth.
///
/// <c>OnTimedOut</c> (task listed this as load-bearing, but the actual decompiled body is
/// <c>AddLog(QuestFailedWithTimeOutLogText)</c> and NOTHING else) is deliberately LEFT UNGATED - harmless,
/// per-peer-local journal text, the exact same "leave the bare AddLog ungated" reasoning
/// <c>HeadmanNeedsGrainOwnershipGatePatches</c>' doc comment already documents for that quest type's own
/// <c>TimeoutFail</c>/plain-<c>AddLog</c> split. <c>OnBeforeTimedOut</c>, by contrast, genuinely IS load-bearing:
/// unconditionally sets <c>RelationshipChangeWithQuestGiver = -5</c> and applies a direct
/// <c>TraitLevelingHelper.OnIssueSolvedThroughQuest</c> Honor -30 mutation to the shared QuestGiver Hero, reached
/// via ambient <c>QuestManager.HourlyTick</c>-driven due-time machinery that is not host/client-gated anywhere
/// else in this codebase - every peer's own mirrored quest independently reaching its due time at the same
/// in-game moment would otherwise double/triple-apply this, the same shape
/// <c>LandLordTheArtOfTheTradeOwnershipGatePatches.OnTimedOutPrefix</c>'s doc comment documents for that (BOTH
/// mutation and completion, not just journal text) <c>OnTimedOut</c> override. Gated below.
///
/// Everything else (the village-event submenus registered via <c>AddVillageEvents</c>/the "village_collect_revenue"
/// wait-menu/<c>talk_to_steward_on_condition</c>) is reachable ONLY through <c>ProgressRevenueCollectionForVillage</c>,
/// itself only ever called from the already-gated <c>HourlyTick</c> below (traced: <c>GameMenu.SwitchToMenu</c>
/// for every village-event id has exactly one call site, inside that method) - redundant defense-in-depth, not
/// required, matching the task's own explicit exclusion for this shape.
/// </summary>
[HarmonyPatch(typeof(RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest))]
internal class RevenueFarmingOwnershipGatePatches
{
    // The only place that advances collection progress, gives gold, and spawns village-event menus.
    [HarmonyPatch("HourlyTick")]
    [HarmonyPrefix]
    private static bool HourlyTickPrefix(RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("QuestCompletedWithSuccess")]
    [HarmonyPrefix]
    private static bool QuestCompletedWithSuccessPrefix(RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("QuestCompletedWithBetray")]
    [HarmonyPrefix]
    private static bool QuestCompletedWithBetrayPrefix(RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("OnBeforeTimedOut")]
    [HarmonyPrefix]
    private static bool OnBeforeTimedOutPrefix(RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("OnWarDeclared")]
    [HarmonyPrefix]
    private static bool OnWarDeclaredPrefix(RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("OnClanChangedKingdom")]
    [HarmonyPrefix]
    private static bool OnClanChangedKingdomPrefix(RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("OnVillageRaid")]
    [HarmonyPrefix]
    private static bool OnVillageRaidPrefix(RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("OnMapEventStarted")]
    [HarmonyPrefix]
    private static bool OnMapEventStartedPrefix(RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("OnSettlementOwnerChanged")]
    [HarmonyPrefix]
    private static bool OnSettlementOwnerChangedPrefix(RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    // Defense-in-depth only - see the type doc comment for why these two are provably inert on a non-owner
    // mirror (both only ever flip CollectingRevenues, read exclusively inside the already-gated HourlyTick
    // chain above).
    [HarmonyPatch("OnMapEventEnded")]
    [HarmonyPrefix]
    private static bool OnMapEventEndedPrefix(RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("OnPlayerDeserted")]
    [HarmonyPrefix]
    private static bool OnPlayerDesertedPrefix(RevenueFarmingIssueBehavior.RevenueFarmingIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);
}
