using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Independent re-verification finding (standing-lesson re-check, item (b)): this quest converges on TWO
/// completion methods, not one - <c>OnQuestSucceeded()</c> (the fight-win path, and the "protect the caravan"
/// path via the caravaneer's own gratitude dialogue) and <c>OnPlayerHiredBandits()</c> (the alternate
/// "recruit the bandits outright" success path, wired to <c>CampaignEvents.BanditPartyRecruited</c>). Both are
/// structurally Category A (only ever wired via <c>RegisterEvents()</c>, itself only ever called by
/// <c>StartQuest()</c> - confirmed directly against the decompiled <c>QuestBase</c> source, which calls
/// <c>RegisterEvents()</c> from exactly two places: <c>StartQuest()</c> (genuine-accepter-only) and
/// <c>InitializeQuestOnLoadWithQuestManager()</c> (save/load resume, itself only invoked by
/// <c>QuestManager</c> for genuinely <c>IsOngoing</c> quests - shared, already-audited infrastructure, not
/// re-derived here)) - matching the reasoning <c>SmugglersQuestOwnershipGatePatch</c>'s own doc comment already
/// gives for its equivalent fight-win path.
///
/// Gated anyway, defense-in-depth, for the same reason established by EVERY other ownership-gate precedent
/// this session (Smugglers/GangLeaderNeedsWeapons/LordWantsRivalCaptured/RevenueFarming/ArtisanOverpricedGoods):
/// "gate the actual mutation, not each registration/condition site". Concretely: <c>OnQuestSucceeded()</c> is
/// ALSO reachable as the <c>Consequence</c> of <c>GetCaravaneerDialogFlow()</c>'s own dynamically-registered
/// "start"-state dialogue (<c>Campaign.Current.ConversationManager.AddDialogFlow(GetCaravaneerDialogFlow(), this)</c>,
/// called unconditionally from <c>SetDialogs()</c> in the ctor, on EVERY peer's own mirror - Category B). That
/// dialogue's own Condition requires <c>_isCaravanSaved == true</c>, which today only the OWNER's own
/// <c>MapEventEnded</c> handler ever sets (so a non-owner's mirror never observes it as true, since that
/// handler is Category A) - but this is an incidental, fragile safety, not a structural one (e.g. it does not
/// hold across a save/reload cycle that re-wires <c>RegisterEvents()</c> on every peer's own mirror per
/// <c>InitializeQuestOnLoadWithQuestManager</c>'s general shape). Gating the leaf method itself removes the
/// dependency on that incidental safety entirely, exactly matching this session's established rule.
///
/// <c>CompleteQuestWithSuccess()</c> is called from ONLY these two methods in this Quest class (no
/// Artisan-Overpriced-Goods-shaped "chained external Consequence" gap - confirmed by direct read, item (c) of
/// the standing-lesson re-check) - gating both closes every path.
///
/// The failure/cancel paths (<c>OnFailedVicinityChecks</c>/<c>OnCaravanDestroyed</c>/
/// <c>OnCaravanSurvivedWithoutHelp</c>/<c>OnTimedOut</c>/<c>CheckFailureDueToDiplomaticState</c>) are
/// deliberately NOT gated - every one of them is reachable ONLY from <c>HourlyTick()</c> or a
/// RegisterEvents-wired listener (<c>MapEventEnded</c>/<c>SettlementEntered</c>/<c>WarDeclared</c>/
/// <c>OnClanChangedKingdom</c>), all Category A, with no Category B (dialogue) entry point at all - same
/// reasoning already established for <c>LordWantsRivalCapturedOwnershipGatePatches</c>' non-gated
/// <c>FirstCounterOfferFinished()</c> and Smugglers' non-gated <c>FailQuest</c>.
/// </summary>
[HarmonyPatch(typeof(CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest))]
internal class CaravanAmbushQuestOwnershipGatePatch
{
    [HarmonyPatch("OnQuestSucceeded")]
    [HarmonyPrefix]
    private static bool OnQuestSucceededPrefix(CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("OnPlayerHiredBandits")]
    [HarmonyPrefix]
    private static bool OnPlayerHiredBanditsPrefix(CaravanAmbushIssueBehavior.CaravanAmbushIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);
}
