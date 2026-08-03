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
/// Same bug/fix shape as <see cref="VillageNeedsToolsAlternativeSolutionOwnershipGatePatch"/>/
/// <see cref="VillageNeedsCraftingMaterialsAlternativeSolutionOwnershipGatePatch"/> (see the former's doc
/// comment for the full derivation), consolidated into ONE shared patch/HourlyTick-scan pair covering every
/// NEW issue type that has <c>IsThereAlternativeSolution == true</c>: <c>LordNeedsHorsesIssue</c>,
/// <c>CapturedByBountyHuntersIssue</c>, <c>LandlordTrainingForRetainersIssue</c>,
/// <c>GangLeaderNeedsRecruitsIssue</c>, and (Tier 1 Group 1B) <c>LandLordNeedsManualLaborersIssue</c>,
/// <c>HeadmanVillageNeedsDraughtAnimalsIssue</c>, <c>LordNeedsGarrisonTroopsIssue</c> - all seven need the
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
