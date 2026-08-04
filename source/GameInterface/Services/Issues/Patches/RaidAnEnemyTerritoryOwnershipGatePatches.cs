using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Turn-in/reload ownership gate, matching <c>EscortMerchantCaravanOwnershipGatePatches</c>'s established shape
/// (see that type's own doc comment for the fuller precedent derivation this one follows).
///
/// Two independently-found gaps, both beyond <see cref="RaidAnEnemyTerritoryRaidCompletionPatches"/>'s own
/// raid-tracking fix:
///
/// (1) TURN-IN: <c>RaidAnEnemyTerritoryQuest.SetDialogs()</c> (which builds <c>DiscussDialogFlow</c>, including
/// the "Yes, I raided {RAIDED_VILLAGE_COUNT} villages" success option whose <c>Consequence</c> calls
/// <c>MainHeroRaidedAllVillages()</c> - the actual gold/renown/trait/relation payout) is called
/// UNCONDITIONALLY from the quest's own constructor, which - per
/// <see cref="GenericAcceptMirrorIssueTypes.QuestSolutionMirrorEligible"/> - runs on EVERY peer's own mirror
/// quest object, not just the real accepter's. Once <see cref="RaidAnEnemyTerritoryRaidCompletionPatches"/>'
/// own broadcast keeps <c>_raidedVillages</c> in sync across every peer (a prerequisite for the real owner's
/// OWN turn-in to work at all), the option's <c>_raidedVillages.Count &gt;= 3</c> condition trips identically
/// on every peer's own mirror too - so ANY player who happens to walk up to the (id-matched) quest giver Hero
/// on their own local game and select this option would wrongly pay THEMSELVES, using their own local
/// <c>Hero.MainHero</c>/<c>Clan.PlayerClan</c> - the exact turn-in ownership gap shape found in nearly every
/// other quest type this session.
///
/// (2) RELOAD/JOIN: <c>QuestManager.OnQuestStarted</c> (what actually adds a quest to the iterated
/// <c>QuestManager.Quests</c> collection <c>DailyTick</c>/<c>OnTimedOut</c> are driven from) is itself only ever
/// called from the real, live-dialogue-only <c>StartQuest()</c> - so under ordinary play, on a non-owner's own
/// mirror, these ARE structurally unreachable, and the same is true of the <c>CampaignEvents</c>-listener-driven
/// methods below (<c>RegisterEvents()</c> is likewise only wired there). But (matching
/// <c>EscortMerchantCaravanOwnershipGatePatches</c>'/<c>HeadmanNeedsToDeliverAHerdOwnershipGatePatches</c>' own
/// established precedent) a full <c>TransferSaveState</c> save-state transfer to a newly joining client DOES
/// restore <c>QuestManager.Quests</c> - and hence re-wires everything below - for that joining peer too, even
/// when they are not this quest's real owner.
///
/// Gating <c>MainHeroRaidedAllVillages</c> alone would already close every path THAT method is reachable from
/// (the dialogue Consequence directly, plus every listener/tick method's own "already met threshold" branch),
/// but the individual listener/tick entry points are gated too, matching precedent exactly - each also guards
/// its own OTHER (below-threshold) branch, which calls a DIFFERENT private leaf
/// (<c>QuestGiverDied</c>/<c>EnemyIsOutOfVillages</c>/<c>DeclaredPeaceBetweenQuestGiverAndEnemyFactions</c>/
/// <c>FactionLeft</c>/<c>QuestHelper.CheckWarDeclarationAndFailOrCancelTheQuest</c>) that would otherwise remain
/// ungated.
/// </summary>
[HarmonyPatch(typeof(RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest))]
internal class RaidAnEnemyTerritoryOwnershipGatePatches
{
    [HarmonyPatch("OnHeroKilled")]
    [HarmonyPrefix]
    private static bool OnHeroKilledPrefix(RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest __instance) => Gate(__instance);

    [HarmonyPatch("OnSettlementOwnerChanged")]
    [HarmonyPrefix]
    private static bool OnSettlementOwnerChangedPrefix(RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest __instance) => Gate(__instance);

    [HarmonyPatch("OnWarDeclared")]
    [HarmonyPrefix]
    private static bool OnWarDeclaredPrefix(RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest __instance) => Gate(__instance);

    [HarmonyPatch("OnMakePeace")]
    [HarmonyPrefix]
    private static bool OnMakePeacePrefix(RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest __instance) => Gate(__instance);

    [HarmonyPatch("OnClanChangedKingdom")]
    [HarmonyPrefix]
    private static bool OnClanChangedKingdomPrefix(RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest __instance) => Gate(__instance);

    [HarmonyPatch("DailyTick")]
    [HarmonyPrefix]
    private static bool DailyTickPrefix(RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest __instance) => Gate(__instance);

    [HarmonyPatch("OnTimedOut")]
    [HarmonyPrefix]
    private static bool OnTimedOutPrefix(RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest __instance) => Gate(__instance);

    [HarmonyPatch("OnBeforeTimedOut")]
    [HarmonyPrefix]
    private static bool OnBeforeTimedOutPrefix(RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest __instance) => Gate(__instance);

    /// <summary>The turn-in choke point (gap 1 above) - reachable via the live <c>DiscussDialogFlow</c>
    /// Consequence on EVERY peer, independent of the reload/join-only reachability of the entry points above.</summary>
    [HarmonyPatch("MainHeroRaidedAllVillages")]
    [HarmonyPrefix]
    private static bool MainHeroRaidedAllVillagesPrefix(RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest __instance) => Gate(__instance);

    private static bool Gate(RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(instance.QuestGiver);
}
