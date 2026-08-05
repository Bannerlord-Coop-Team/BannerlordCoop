using Common;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Same bug/fix shape as <see cref="VillageNeedsToolsAlternativeSolutionOwnershipGatePatch"/> (see that type's
/// doc comment for the full derivation), consolidated into ONE shared patch/HourlyTick-scan pair covering
/// every issue type here that has <c>IsThereAlternativeSolution == true</c> and needs no additional
/// per-client-divergent field beyond what <c>MirrorAlternativeAccepted</c> already forces: <c>LordNeedsHorsesIssue</c>,
/// <c>HeadmanVillageNeedsDraughtAnimalsIssue</c>, <c>LandLordTheArtOfTheTradeIssue</c>, <c>HeadmanNeedsGrainIssue</c>,
/// <c>HeadmanNeedsToDeliverAHerdIssue</c>, <c>ArtisanCantSellProductsAtAFairPriceIssue</c>,
/// <c>GangLeaderNeedsToOffloadStolenGoodsIssue</c>, <c>SmugglersIssue</c>, <c>ArtisanOverpricedGoodsIssue</c>,
/// <c>GangLeaderNeedsWeaponsIssue</c> - all need the identical fix (gate the real
/// <c>CompleteIssueWithAlternativeSolution</c> consequence to the recorded owner only, plus an
/// unconditional, ownership-self-limiting <c>HourlyTickEvent</c> listener so the machine that actually needs
/// to trigger it - very often a remote client, not the server - has a reason to). Written once here instead
/// of as ten more near-identical copies of Village Needs Tools' own two-class pattern; Tools' existing
/// patch is left untouched (independently verified, no need to fold it into this shared version).
/// </summary>
[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithAlternativeSolution))]
internal class NewIssueTypesAlternativeSolutionOwnershipGatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase __instance)
    {
        if (!GenericAcceptMirrorIssueTypes.AlternativeSolutionMirrorEligible.Contains(__instance.GetType())) return true;

        return VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.IssueOwner);
    }
}

/// <summary>See <see cref="NewIssueTypesAlternativeSolutionOwnershipGatePatch"/>'s doc comment.</summary>
[HarmonyPatch]
internal class NewIssueTypesAlternativeSolutionCompletionPatches
{
    [HarmonyPatch(typeof(LordNeedsHorsesIssueBehavior), nameof(LordNeedsHorsesIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void LordNeedsHorsesRegisterEventsPostfix(LordNeedsHorsesIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    [HarmonyPatch(typeof(HeadmanVillageNeedsDraughtAnimalsIssueBehavior), nameof(HeadmanVillageNeedsDraughtAnimalsIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void HeadmanVillageNeedsDraughtAnimalsRegisterEventsPostfix(HeadmanVillageNeedsDraughtAnimalsIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    [HarmonyPatch(typeof(LandLordTheArtOfTheTradeIssueBehavior), nameof(LandLordTheArtOfTheTradeIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void LandLordTheArtOfTheTradeRegisterEventsPostfix(LandLordTheArtOfTheTradeIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    // Village Needs Grain Seeds - same shared choke point, no new per-type file needed.
    // HeadmanNeedsGrainIssue.AlternativeSolutionScaleFlags is Duration only (confirmed against the decompiled
    // source - no FailureRisk flag), so like every type here except CapturedByBountyHunters (not in this
    // branch's quest set), its alternative solution always succeeds deterministically:
    // AlternativeSolutionEndWithFailureConsequence exists on the Issue class but is unreachable in practice
    // (IssueBase._failureChance only becomes nonzero when AlternativeSolutionScaleFlags has FailureRisk), so
    // routing it through this generic, success-only trigger is safe.
    [HarmonyPatch(typeof(HeadmanNeedsGrainIssueBehavior), nameof(HeadmanNeedsGrainIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void HeadmanNeedsGrainRegisterEventsPostfix(HeadmanNeedsGrainIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    // Deliver the Herd to Town - same shared choke point, no new per-type file needed.
    // HeadmanNeedsToDeliverAHerdIssue.AlternativeSolutionScaleFlags is Duration only (confirmed against the
    // decompiled source), so like Grain Seeds above, its alternative solution always succeeds deterministically
    // and routing it through this generic, success-only trigger is safe.
    [HarmonyPatch(typeof(HeadmanNeedsToDeliverAHerdIssueBehavior), nameof(HeadmanNeedsToDeliverAHerdIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void HeadmanNeedsToDeliverAHerdRegisterEventsPostfix(HeadmanNeedsToDeliverAHerdIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    // Tier 2 Group B additions - same shared choke point, no new per-type file needed. Both confirmed
    // AlternativeSolutionScaleFlags == None (Artisan does not override it; Gang Leader does not either - see
    // GenericAcceptMirrorIssueTypes's doc comment), so both always succeed deterministically.
    [HarmonyPatch(typeof(ArtisanCantSellProductsAtAFairPriceIssueBehavior), nameof(ArtisanCantSellProductsAtAFairPriceIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void ArtisanCantSellProductsAtAFairPriceRegisterEventsPostfix(ArtisanCantSellProductsAtAFairPriceIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    [HarmonyPatch(typeof(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior), nameof(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void GangLeaderNeedsToOffloadStolenGoodsRegisterEventsPostfix(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    // Tier 2 Group A - Smugglers. AlternativeSolutionScaleFlags is Casualties | FailureRisk (confirmed against
    // the decompiled source) - genuinely can fail, still safe to route through this generic,
    // ownership-self-limiting trigger.
    [HarmonyPatch(typeof(SmugglersIssueBehavior), nameof(SmugglersIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void SmugglersRegisterEventsPostfix(SmugglersIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    // Tier 2 Group B (continued) - Artisan Overpriced Goods. AlternativeSolutionScaleFlags is Duration only
    // (confirmed against the decompiled source - not overridden beyond the base Duration flag), so it always
    // succeeds deterministically, same as ArtisanCantSellProductsAtAFairPrice above.
    [HarmonyPatch(typeof(ArtisanOverpricedGoodsIssueBehavior), nameof(ArtisanOverpricedGoodsIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void ArtisanOverpricedGoodsRegisterEventsPostfix(ArtisanOverpricedGoodsIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    // Tier 2 Group A - Gang Leader Needs Weapons. AlternativeSolutionScaleFlags is Duration only (confirmed
    // against the decompiled source - no FailureRisk/Casualties), so it always succeeds deterministically, same
    // as ArtisanCantSellProductsAtAFairPrice/ArtisanOverpricedGoods above.
    [HarmonyPatch(typeof(GangLeaderNeedsWeaponsIssueQuestBehavior), nameof(GangLeaderNeedsWeaponsIssueQuestBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void GangLeaderNeedsWeaponsRegisterEventsPostfix(GangLeaderNeedsWeaponsIssueQuestBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    private static void OnHourlyTick()
    {
        if (Campaign.Current?.IssueManager == null) return;

        // Snapshot first: a genuine completion mutates IssueManager.Issues (removes the finalized entry), and
        // MBReadOnlyDictionary's own enumerator doesn't tolerate that mid-iteration.
        var snapshot = new List<KeyValuePair<Hero, IssueBase>>();
        foreach (var kvp in Campaign.Current.IssueManager.Issues)
        {
            snapshot.Add(kvp);
        }

        foreach (var kvp in snapshot)
        {
            if (!GenericAcceptMirrorIssueTypes.AlternativeSolutionMirrorEligible.Contains(kvp.Value.GetType())) continue;
            if (!VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(kvp.Key)) continue;

            TryTriggerOwnedAlternativeSolutionCompletion(kvp.Value);
        }
    }

    /// <summary>
    /// Generic equivalent of e.g. <c>VillageNeedsToolsIssueInterface.TryTriggerOwnedAlternativeSolutionCompletion</c>
    /// - safe to share across every type here because none of it is type-specific: every
    /// <see cref="IssueBase"/> already exposes <c>IsSolvingWithAlternative</c>/<c>AlternativeSolutionReturnTimeForTroops</c>/
    /// <c>CompleteIssueWithAlternativeSolution</c> publicly, and every type registered above is confirmed
    /// deterministic-success (no <c>AlternativeSolutionScaleFlag.FailureRisk</c>) except Smugglers, which
    /// genuinely can fail - see that type's own registration comment above for why that's still fine to route
    /// through this same generic trigger: a failure just produces <c>VillageIssueFinalizeReason.IssueOnly</c>
    /// via <see cref="IssueManagerQuestCompletedReasonCapture"/>'s fallback instead of
    /// <c>AlternativeSolutionSuccess</c> - a cosmetic label difference only, harmless.
    /// </summary>
    private static void TryTriggerOwnedAlternativeSolutionCompletion(IssueBase issue)
    {
        if (!issue.IsSolvingWithAlternative || !issue.AlternativeSolutionReturnTimeForTroops.IsPast) return;

        IssueManagerQuestCompletedReasonCapture.PendingReasons[issue.IssueOwner] = VillageIssueFinalizeReason.AlternativeSolutionSuccess;

        issue.CompleteIssueWithAlternativeSolution(); // genuine call - NOT under AllowedThread, this is the real trigger
    }
}
