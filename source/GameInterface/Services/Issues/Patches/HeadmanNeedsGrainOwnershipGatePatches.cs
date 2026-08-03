using GameInterface.Services.Issues.Interfaces;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Bug-1-shaped fix (see <see cref="VillageNeedsToolsQuestOwnershipGatePatch"/>'s doc comment for the full
/// derivation), for <c>HeadmanNeedsGrainIssueQuest</c> - the one type in this project whose completion trigger
/// is NOT a direct in-conversation <c>Consequence</c> delegate: the "Yes. Here is your grain." player option's
/// own consequence only does <c>Campaign.Current.ConversationManager.ConversationEndOneShot += Success;</c>,
/// so the actual reward-applying <c>Success()</c> call happens AFTER the conversation window has already
/// closed, on a deferred one-shot callback (see the decompiled <c>SetDialogs</c>). There is no earlier named
/// method to gate here the way e.g. <c>VillageNeedsToolsIssueQuest.PlayerHasTools</c> is gated - the
/// anonymous <c>delegate { += Success; }</c> registered inline by <c>.Consequence(...)</c> is a compiler-generated
/// closure with no stable Harmony target. <c>ConversationEndOneShot</c> is itself a local, per-process
/// <c>ConversationManager</c> callback (never networked), so it only ever fires for whichever peer's OWN
/// conversation just ended - no cross-peer leakage - but a non-owner whose own mirrored quest object
/// independently satisfies <c>CompleteQuestClickableConditions</c> (checks only their own local grain roster,
/// no ownership concept) can still select the option and register the same callback on their own machine.
/// The gate therefore has to sit on <c>Success()</c> itself: it is the one guaranteed choke point regardless of
/// when/how the deferred callback actually fires, and (since <see cref="VillageNeedsToolsIssueOwnership"/> is
/// populated at ACCEPT time, days/weeks of in-game time before any realistic turn-in) there is no meaningful
/// TOCTOU window to worry about at this later point.
///
/// <c>CompleteQuestClickableConditions</c> is ALSO gated here (same shape as
/// <c>VillageNeedsToolsQuestOwnershipGatePatch</c>/<c>ArmyNeedsSuppliesOwnershipGatePatches</c>) purely for UX
/// parity - hiding the option entirely for a non-owner - but this is redundant belt-and-braces, not the load
/// bearing fix: even if it were skipped, <c>Success()</c>'s own gate below fully prevents the reward from ever
/// being applied twice.
///
/// <c>TimeoutFail()</c> and <c>CriminalRatingFail()</c> are gated for the same reason as
/// <c>LandLordTheArtOfTheTradeOwnershipGatePatches.OnTimedOutPrefix</c>: both are reached via ambient,
/// campaign-state-driven triggers (<c>OnTimedOut</c> via <c>QuestManager.HourlyTick</c>; <c>CriminalRatingFail</c>
/// via the shared <c>WarDeclared</c> event - see the <c>CausedByCrimeRatingChange</c> branch, which has NO
/// <c>MobileParty.MainParty</c> condition at all, unlike the raid branch beside it) that are not host/client-gated
/// anywhere in this codebase, so every peer's own mirrored quest independently reaches them and would otherwise
/// apply the QuestGiver-shared power/prosperity/relation mutation once per connected peer instead of once, total.
/// <c>OnTimedOut</c>'s own <c>AddLog(QuestTimeoutLogText)</c> call is deliberately left un-gated (harmless,
/// per-peer-local journal text, matching every other type's informational logs) - only <c>TimeoutFail()</c>
/// itself (the real state mutation) is gated, so the journal entry still shows correctly on every peer.
/// </summary>
[HarmonyPatch(typeof(HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssueQuest))]
internal class HeadmanNeedsGrainOwnershipGatePatches
{
    [HarmonyPatch("CompleteQuestClickableConditions")]
    [HarmonyPrefix]
    private static bool CompleteQuestClickableConditionsPrefix(
        HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssueQuest __instance,
        ref bool __result,
        out TextObject explanation)
    {
        if (!VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver))
        {
            __result = false;
            // Reuses the exact same localized string the real method itself falls back to when the (real,
            // owner-only) grain-count check fails - see the decompiled source's own
            // CompleteQuestClickableConditions.
            explanation = new TextObject("{=mzabdwoh}You don't have enough grain.");
            return false; // skip the original - non-owners never see/can-select "Yes. Here is your grain."
        }

        explanation = null;
        return true; // real owner: run the real check unmodified
    }

    [HarmonyPatch("Success")]
    [HarmonyPrefix]
    private static bool SuccessPrefix(HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("TimeoutFail")]
    [HarmonyPrefix]
    private static bool TimeoutFailPrefix(HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);

    [HarmonyPatch("CriminalRatingFail")]
    [HarmonyPrefix]
    private static bool CriminalRatingFailPrefix(HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssueQuest __instance) =>
        VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.QuestGiver);
}
