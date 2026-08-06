using Common;
using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Issues.Interfaces;
using GameInterface.Services.Issues.Messages;
using HarmonyLib;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Patches;

/// <summary>
/// Same bug/fix shape as <see cref="VillageNeedsToolsAlternativeSolutionOwnershipGatePatch"/>/
/// <see cref="VillageNeedsCraftingMaterialsAlternativeSolutionOwnershipGatePatch"/> (see the former's doc
/// comment for the full derivation), consolidated into ONE shared patch/HourlyTick-scan pair covering every
/// NEW issue type that has <c>IsThereAlternativeSolution == true</c>: <c>LordNeedsHorsesIssue</c>,
/// <c>CapturedByBountyHuntersIssue</c>, <c>LandlordTrainingForRetainersIssue</c>,
/// <c>GangLeaderNeedsRecruitsIssue</c>, and (Tier 1 Group 1B) <c>LandLordNeedsManualLaborersIssue</c>,
/// <c>HeadmanVillageNeedsDraughtAnimalsIssue</c>, <c>LordNeedsGarrisonTroopsIssue</c>, and (Tier 1 Group 1C/1D
/// plus Village Needs Grain Seeds) <c>NearbyBanditBaseIssue</c>, <c>LandLordTheArtOfTheTradeIssue</c>,
/// <c>RuralNotableInnAndOutIssue</c>, <c>ProdigalSonIssue</c>, <c>TheSpyPartyIssue</c>,
/// <c>HeadmanNeedsGrainIssue</c> - all need the
/// identical fix (gate the real
/// <c>CompleteIssueWithAlternativeSolution</c> consequence to the recorded owner only, plus an
/// unconditional, ownership-self-limiting <c>HourlyTickEvent</c> listener so the machine that actually needs
/// to trigger it - very often a remote client, not the server - has a reason to). Written once here instead
/// of as four more near-identical copies of Tools'/Crafting Materials' own two-class pattern; Tools' and
/// Crafting Materials' existing patches are left untouched (independently verified, no need to fold them into
/// this shared version).
/// </summary>
[HarmonyPatch(typeof(IssueBase), nameof(IssueBase.CompleteIssueWithAlternativeSolution))]
internal class NewIssueTypesAlternativeSolutionOwnershipGatePatch
{
    [HarmonyPrefix]
    private static bool Prefix(IssueBase __instance)
    {
        if (!GenericAcceptMirrorIssueTypes.AlternativeSolutionMirrorEligible.Contains(__instance.GetType())) return true;

        return VillageNeedsToolsIssueOwnership.IsLocalPeerOwner(__instance.IssueOwner)
            || AlternativeSolutionCompletionAuthorityGuard.IsActive;
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

    [HarmonyPatch(typeof(CapturedByBountyHuntersIssueBehavior), nameof(CapturedByBountyHuntersIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void CapturedByBountyHuntersRegisterEventsPostfix(CapturedByBountyHuntersIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    [HarmonyPatch(typeof(LandlordTrainingForRetainersIssueBehavior), nameof(LandlordTrainingForRetainersIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void LandlordTrainingForRetainersRegisterEventsPostfix(LandlordTrainingForRetainersIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    [HarmonyPatch(typeof(GangLeaderNeedsRecruitsIssueBehavior), nameof(GangLeaderNeedsRecruitsIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void GangLeaderNeedsRecruitsRegisterEventsPostfix(GangLeaderNeedsRecruitsIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    // Tier 1 Group 1B additions - same shared choke point, no new per-type file needed.
    [HarmonyPatch(typeof(LandLordNeedsManualLaborersIssueBehavior), nameof(LandLordNeedsManualLaborersIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void LandLordNeedsManualLaborersRegisterEventsPostfix(LandLordNeedsManualLaborersIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    [HarmonyPatch(typeof(HeadmanVillageNeedsDraughtAnimalsIssueBehavior), nameof(HeadmanVillageNeedsDraughtAnimalsIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void HeadmanVillageNeedsDraughtAnimalsRegisterEventsPostfix(HeadmanVillageNeedsDraughtAnimalsIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    [HarmonyPatch(typeof(LordNeedsGarrisonTroopsIssueQuestBehavior), nameof(LordNeedsGarrisonTroopsIssueQuestBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void LordNeedsGarrisonTroopsRegisterEventsPostfix(LordNeedsGarrisonTroopsIssueQuestBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    // Tier 1 Group 1C/1D additions - same shared choke point, no new per-type file needed. RuralNotableInnAndOut/
    // ProdigalSon/TheSpyParty are SandBox.dll types (this project's first cross-assembly issue types) - the
    // compile-time-typed [HarmonyPatch(typeof(...))] attribute works identically regardless of which assembly
    // the behavior lives in, since GameInterface.csproj already references SandBox.dll directly (see
    // DisableAllIssueBehaviorsExceptAllowlist's doc comment).
    [HarmonyPatch(typeof(NearbyBanditBaseIssueBehavior), nameof(NearbyBanditBaseIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void NearbyBanditBaseRegisterEventsPostfix(NearbyBanditBaseIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    [HarmonyPatch(typeof(LandLordTheArtOfTheTradeIssueBehavior), nameof(LandLordTheArtOfTheTradeIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void LandLordTheArtOfTheTradeRegisterEventsPostfix(LandLordTheArtOfTheTradeIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    [HarmonyPatch(typeof(SandBox.Issues.RuralNotableInnAndOutIssueBehavior), nameof(SandBox.Issues.RuralNotableInnAndOutIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void RuralNotableInnAndOutRegisterEventsPostfix(SandBox.Issues.RuralNotableInnAndOutIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    [HarmonyPatch(typeof(SandBox.Issues.ProdigalSonIssueBehavior), nameof(SandBox.Issues.ProdigalSonIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void ProdigalSonRegisterEventsPostfix(SandBox.Issues.ProdigalSonIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    [HarmonyPatch(typeof(SandBox.Issues.TheSpyPartyIssueQuestBehavior), nameof(SandBox.Issues.TheSpyPartyIssueQuestBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void TheSpyPartyRegisterEventsPostfix(SandBox.Issues.TheSpyPartyIssueQuestBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    // Village Needs Grain Seeds - same shared choke point, no new per-type file needed.
    // HeadmanNeedsGrainIssue.AlternativeSolutionScaleFlags is Duration only (confirmed against the decompiled
    // source - no FailureRisk flag), so like every type above except CapturedByBountyHunters, its alternative
    // solution always succeeds deterministically: AlternativeSolutionEndWithFailureConsequence exists on the
    // Issue class but is unreachable in practice (IssueBase._failureChance only becomes nonzero when
    // AlternativeSolutionScaleFlags has FailureRisk), so routing it through this generic, success-only trigger
    // is safe.
    [HarmonyPatch(typeof(HeadmanNeedsGrainIssueBehavior), nameof(HeadmanNeedsGrainIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void HeadmanNeedsGrainRegisterEventsPostfix(HeadmanNeedsGrainIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    // Deliver the Herd to Town - same shared choke point, no new per-type file needed.
    // HeadmanNeedsToDeliverAHerdIssue.AlternativeSolutionScaleFlags is Duration only (confirmed against the
    // decompiled source), so like Grain Seeds above, its alternative solution always succeeds deterministically
    // and routing it through this generic, success-only trigger is safe. Added by independent review of the
    // Deliver the Herd pass, which found this type was registered in GenericAcceptMirrorIssueTypes'
    // AlternativeSolutionMirrorEligible set but the matching HourlyTick registration here was never carried
    // out - without it, a client-owner's alternative-solution completion could never fire (IssueManager.DailyTick,
    // the only other path to CompleteIssueWithAlternativeSolution, is server-only), leaving the quest
    // permanently stuck in SolvingWithAlternativeSolution for that player.
    [HarmonyPatch(typeof(HeadmanNeedsToDeliverAHerdIssueBehavior), nameof(HeadmanNeedsToDeliverAHerdIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void HeadmanNeedsToDeliverAHerdRegisterEventsPostfix(HeadmanNeedsToDeliverAHerdIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    // Tier 2 Group B additions - same shared choke point, no new per-type file needed. Both confirmed
    // AlternativeSolutionScaleFlags == None (Artisan does not override it; Gang Leader does not either - see
    // GenericAcceptMirrorIssueTypes's doc comment), so both always succeed deterministically. Added alongside
    // the AlternativeSolutionMirrorEligible registration in the same commit this time (see that file's own
    // lesson-learned comment on Deliver the Herd, where the two were split across commits and the HourlyTick
    // registration was initially missed).
    [HarmonyPatch(typeof(ArtisanCantSellProductsAtAFairPriceIssueBehavior), nameof(ArtisanCantSellProductsAtAFairPriceIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void ArtisanCantSellProductsAtAFairPriceRegisterEventsPostfix(ArtisanCantSellProductsAtAFairPriceIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    // GangLeaderNeedsToOffloadStolenGoods is deliberately NOT registered here anymore - see
    // GangLeaderNeedsToOffloadStolenGoodsAlternativeSolutionCompletionPatches for its own dedicated
    // server-authoritative trigger.

    // Tier 2 Group A - Smugglers. AlternativeSolutionScaleFlags is Casualties | FailureRisk (confirmed against
    // the decompiled source), the same shape as CapturedByBountyHunters above - genuinely can fail, still safe
    // to route through this generic, ownership-self-limiting trigger.
    [HarmonyPatch(typeof(SmugglersIssueBehavior), nameof(SmugglersIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void SmugglersRegisterEventsPostfix(SmugglersIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    // Tier 2 Group B (continued) - Artisan Overpriced Goods. AlternativeSolutionScaleFlags is Duration only
    // (confirmed against the decompiled source - not overridden beyond the base Duration flag), so it always
    // succeeds deterministically, same as ArtisanCantSellProductsAtAFairPrice above. Registered in the SAME
    // commit as the AlternativeSolutionMirrorEligible entry (see GenericAcceptMirrorIssueTypes's own
    // lesson-learned comment on Deliver the Herd, where the two were split across commits and this
    // registration was initially missed).
    [HarmonyPatch(typeof(ArtisanOverpricedGoodsIssueBehavior), nameof(ArtisanOverpricedGoodsIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void ArtisanOverpricedGoodsRegisterEventsPostfix(ArtisanOverpricedGoodsIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    // Tier 2 Group A - Caravan Ambush. AlternativeSolutionScaleFlags is Casualties | FailureRisk (confirmed
    // against the decompiled source), the same shape as Smugglers/CapturedByBountyHunters above - genuinely
    // can fail, still safe to route through this generic, ownership-self-limiting trigger.
    [HarmonyPatch(typeof(CaravanAmbushIssueBehavior), nameof(CaravanAmbushIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void CaravanAmbushRegisterEventsPostfix(CaravanAmbushIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    // Tier 2 Group A - Gang Leader Needs Weapons. AlternativeSolutionScaleFlags is Duration only (confirmed
    // against the decompiled source - no FailureRisk/Casualties), so it always succeeds deterministically, same
    // as ArtisanCantSellProductsAtAFairPrice/ArtisanOverpricedGoods above.
    [HarmonyPatch(typeof(GangLeaderNeedsWeaponsIssueQuestBehavior), nameof(GangLeaderNeedsWeaponsIssueQuestBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void GangLeaderNeedsWeaponsRegisterEventsPostfix(GangLeaderNeedsWeaponsIssueQuestBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    // Tier 2 Group A - Merchant Army of Poachers. AlternativeSolutionScaleFlags is Casualties | FailureRisk
    // (confirmed against the decompiled source), the same shape as Smugglers/Caravan Ambush above - genuinely
    // can fail, still safe to route through this generic, ownership-self-limiting trigger.
    [HarmonyPatch(typeof(MerchantArmyOfPoachersIssueBehavior), nameof(MerchantArmyOfPoachersIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void MerchantArmyOfPoachersRegisterEventsPostfix(MerchantArmyOfPoachersIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    // Tier 2 Group A - Escort Merchant Caravan. AlternativeSolutionScaleFlags is Casualties | FailureRisk
    // (confirmed against the decompiled source), the same shape as Smugglers/Caravan Ambush/Merchant Army of
    // Poachers above - genuinely can fail, still safe to route through this generic, ownership-self-limiting
    // trigger.
    [HarmonyPatch(typeof(EscortMerchantCaravanIssueBehavior), nameof(EscortMerchantCaravanIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void EscortMerchantCaravanRegisterEventsPostfix(EscortMerchantCaravanIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    // Rival Gang Moving In - SandBox.dll. AlternativeSolutionScaleFlags is Casualties | FailureRisk (confirmed
    // against the decompiled source), the same shape as Smugglers/Caravan Ambush/Merchant Army of Poachers/
    // Escort Merchant Caravan above - genuinely can fail, still safe to route through this generic,
    // ownership-self-limiting trigger.
    [HarmonyPatch(typeof(SandBox.Issues.RivalGangMovingInIssueBehavior), nameof(SandBox.Issues.RivalGangMovingInIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void RivalGangMovingInRegisterEventsPostfix(SandBox.Issues.RivalGangMovingInIssueBehavior __instance) =>
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(__instance, OnHourlyTick);

    // Snare the Wealthy - SandBox.dll. AlternativeSolutionScaleFlags is Casualties | FailureRisk (confirmed
    // against the decompiled source), the same shape as Smugglers/Caravan Ambush/Merchant Army of Poachers/
    // Escort Merchant Caravan/Rival Gang Moving In above - genuinely can fail, still safe to route through this
    // generic, ownership-self-limiting trigger.
    [HarmonyPatch(typeof(SandBox.Issues.SnareTheWealthyIssueBehavior), nameof(SandBox.Issues.SnareTheWealthyIssueBehavior.RegisterEvents))]
    [HarmonyPostfix]
    private static void SnareTheWealthyRegisterEventsPostfix(SandBox.Issues.SnareTheWealthyIssueBehavior __instance) =>
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
    /// - safe to share across all four types here because none of it is type-specific: every
    /// <see cref="IssueBase"/> already exposes <c>IsSolvingWithAlternative</c>/<c>AlternativeSolutionReturnTimeForTroops</c>/
    /// <c>CompleteIssueWithAlternativeSolution</c> publicly, and the deterministic-success reasoning (no type
    /// here has <c>AlternativeSolutionScaleFlag.FailureRisk</c> EXCEPT <c>CapturedByBountyHuntersIssue</c>,
    /// which genuinely can fail - see its own bespoke Patches file for why that's still fine to route through
    /// this same generic trigger: a failure just produces <c>VillageIssueFinalizeReason.IssueOnly</c> via
    /// <see cref="IssueManagerQuestCompletedReasonCapture"/>'s fallback instead of
    /// <c>AlternativeSolutionSuccess</c> - a cosmetic label difference only, harmless).
    /// </summary>
    private static void TryTriggerOwnedAlternativeSolutionCompletion(IssueBase issue)
    {
        if (!issue.IsSolvingWithAlternative || !issue.AlternativeSolutionReturnTimeForTroops.IsPast) return;

        IssueManagerQuestCompletedReasonCapture.PendingReasons[issue.IssueOwner] = VillageIssueFinalizeReason.AlternativeSolutionSuccess;

        issue.CompleteIssueWithAlternativeSolution(); // genuine call - NOT under AllowedThread, this is the real trigger
    }
}
