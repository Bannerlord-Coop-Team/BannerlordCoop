using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Bug B fix (doc/EscortMerchantCaravan_Design_v2.md §3/§4): <c>QuestManager.OnGameLoaded()</c> ->
/// <c>InitializeQuestOnLoadWithQuestManager()</c> is a second, genuinely-live code path (distinct from the
/// inert-mirror-by-construction case every other Group A quest's tick/listener code already relies on) that DOES
/// call <c>RegisterEvents()</c> (and re-calls <c>SetDialogs()</c>) for real, for whichever peer(s) legitimately
/// have this quest in their own <c>QuestManager.Quests</c> after a reload - the genuine owner's own
/// reconnect/resync (ordinary, frequent - same precedent
/// <see cref="HeadmanNeedsToDeliverAHerdOwnershipGatePatches"/> already established for a different quest type),
/// or a client joining mid-quest via a full <c>TransferSaveState</c> save snapshot.
///
/// Once <c>RegisterEvents()</c> genuinely runs there, every listener it subscribes is live - reading each
/// against the decompiled source turns up unguarded <c>_questCaravanMobileParty</c> dereferences beyond the
/// dialogue conditions <see cref="EscortMerchantCaravanCaravanTalkConditionNullGuardPatch"/> already fixes:
/// <c>OnWarDeclared</c> (fires for ANY war declared anywhere in the campaign), <c>OnPartyHourlyTick</c>'s two
/// callees <c>CheckPartyAndMakeItAttackTheCaravan</c>/<c>CheckEncounterForBanditParty</c> (fires once per active
/// <see cref="MobileParty"/> on the map, per hour), and the quest's own <c>HourlyTick()</c> override. Beyond the
/// NRE risk, having more than one peer's own mirror with live listeners means each would independently perform
/// the SAME world-mutating actions (<c>ActivateBanditParty()</c>'s id-colliding bandit party, gold/relation/
/// prosperity payouts) - a correctness bug independent of whether any dereference actually throws.
///
/// <c>OnTimedOut()</c> is reached via a separate trigger mechanism (<c>QuestManager.HourlyTick()</c>'s own
/// per-quest <c>QuestDueTime.IsPast</c> sweep, not a <c>CampaignEvents</c> listener), but needs the exact same
/// gate for the exact same reason: under ordinary play a non-owner's mirror is never in
/// <c>QuestManager.Quests</c> at all (structurally can't reach it), but after a reload/join more than one peer's
/// own <c>Quests</c> can legitimately hold this quest, and each would independently call <c>OnTimedOut()</c>.
///
/// Once gated to the single recorded owner, that owner's own local vanilla code is what actually mutates
/// <c>Hero.AddPower</c>/relationship/<c>Town.Prosperity</c> - ordinary AutoSync-tracked campaign state that
/// propagates to every other peer as a plain field-value broadcast (the same "one authoritative local execution,
/// then propagation of its result" shape every other synced mutation in this codebase already rides on) - NOT a
/// bespoke Issues-level network message, and not "every peer independently re-derives the same outcome".
///
/// Matches <see cref="HeadmanNeedsToDeliverAHerdOwnershipGatePatches"/>'s established pattern, just applied to a
/// larger method set here because this quest's entire lifecycle (not just its turn-in) is ambient-tick/event-
/// driven.
/// </summary>
[HarmonyPatch(typeof(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest))]
internal class EscortMerchantCaravanOwnershipGatePatches
{
    [HarmonyPatch("OnSettlementEntered")]
    [HarmonyPrefix]
    private static bool OnSettlementEnteredPrefix(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest __instance) => Gate(__instance);

    [HarmonyPatch("OnSettlementLeft")]
    [HarmonyPrefix]
    private static bool OnSettlementLeftPrefix(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest __instance) => Gate(__instance);

    [HarmonyPatch("OnMapEventEnded")]
    [HarmonyPrefix]
    private static bool OnMapEventEndedPrefix(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest __instance) => Gate(__instance);

    [HarmonyPatch("OnWarDeclared")]
    [HarmonyPrefix]
    private static bool OnWarDeclaredPrefix(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest __instance) => Gate(__instance);

    [HarmonyPatch("OnClanChangedKingdom")]
    [HarmonyPrefix]
    private static bool OnClanChangedKingdomPrefix(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest __instance) => Gate(__instance);

    [HarmonyPatch("OnPartyHourlyTick")]
    [HarmonyPrefix]
    private static bool OnPartyHourlyTickPrefix(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest __instance) => Gate(__instance);

    [HarmonyPatch("OnSettlementOwnerChanged")]
    [HarmonyPrefix]
    private static bool OnSettlementOwnerChangedPrefix(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest __instance) => Gate(__instance);

    [HarmonyPatch("HourlyTick")]
    [HarmonyPrefix]
    private static bool HourlyTickPrefix(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest __instance) => Gate(__instance);

    [HarmonyPatch("DailyTick")]
    [HarmonyPrefix]
    private static bool DailyTickPrefix(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest __instance) => Gate(__instance);

    [HarmonyPatch("OnTimedOut")]
    [HarmonyPrefix]
    private static bool OnTimedOutPrefix(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest __instance) => Gate(__instance);

    private static bool Gate(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(instance.QuestGiver);
}

/// <summary>
/// Defensive fallback (doc/EscortMerchantCaravan_Design_v2.md §3.5, open item §7.2 - explicitly flagged there as
/// "not yet proven reachable, needs a live join-mid-quest test", included regardless on a "costs nothing, don't
/// assume the risk is unreachable" basis, same precedent <c>LordNeedsGarrisonTroopsInstanceResolutionPatch</c>'s
/// own doc comment already established): the caravan <see cref="MobileParty"/> itself is guaranteed to already
/// be present and correctly identified on every peer via the generic <c>MobilePartyRegistry</c>/AutoRegistry
/// mechanism, independently of whatever the deserialized <c>_questCaravanMobileParty</c> field happens to say
/// after a reload/join. If that field genuinely resolves correctly (the expected case for a clean save
/// transfer, since it's a real <c>[SaveableField(4)]</c>), this fallback never triggers. If it doesn't, this
/// closes the gap instead of leaving a null field for
/// <see cref="EscortMerchantCaravanOwnershipGatePatches"/>'s owner-only listeners to eventually crash on.
///
/// TIGHTENED beyond the design doc's own illustrative snippet: gated to
/// <see cref="VillageNeedsToolsIssueOwnership.IsLocalPeerOwner"/> rather than running unconditionally - the
/// fallback is only ever useful for the genuine owner (resuming their own tracking correctly); for a non-owner,
/// every dangerous read of <c>_questCaravanMobileParty</c> is already blocked by
/// <see cref="EscortMerchantCaravanOwnershipGatePatches"/> regardless of whether this field ever resolves on
/// their mirror, so running the resolution there would be harmless but pointless work.
/// </summary>
[HarmonyPatch(typeof(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest), "InitializeQuestOnGameLoad")]
internal class EscortMerchantCaravanGameLoadCaravanPartyFallbackPatch
{
    [HarmonyPrefix]
    private static void Prefix(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssueQuest __instance)
    {
        if (!VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver)) return;

        var questCaravanMobilePartyField = AccessTools.Field(__instance.GetType(), "_questCaravanMobileParty");
        if (questCaravanMobilePartyField.GetValue(__instance) != null) return;
        if (!__instance.IsOngoing) return;

        var questGiver = __instance.QuestGiver;
        if (questGiver == null) return;

        var resolved = MobileParty.All.FirstOrDefault(mp =>
            mp.PartyComponent is CustomPartyComponent cpc &&
            cpc.PartyOwner == questGiver &&
            mp.HomeSettlement == questGiver.CurrentSettlement);

        if (resolved != null)
        {
            questCaravanMobilePartyField.SetValue(__instance, resolved);
        }
    }
}
