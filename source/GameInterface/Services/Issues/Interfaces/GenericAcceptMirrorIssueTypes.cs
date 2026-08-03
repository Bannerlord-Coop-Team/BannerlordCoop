using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Interfaces;

/// <summary>
/// Registries of issue types that can ride the EXISTING generic "quest-solution accept"/"alternative-solution
/// accept" mirror mechanism (<see cref="Patches.IssueAcceptancePatches"/> +
/// <see cref="VillageNeedsToolsIssueInterface.MirrorQuestAccepted"/>/<see cref="VillageNeedsToolsIssueInterface.MirrorAlternativeAccepted"/>)
/// unchanged, instead of needing their own bespoke accept-capture-and-force-write mechanism the way
/// <c>VillageNeedsCraftingMaterialsIssueBehavior</c> needed for its per-client accept-time re-derived
/// required-amount/reward.
///
/// A type belongs in <see cref="QuestSolutionMirrorEligible"/> only if EVERY field its
/// <c>GenerateIssueQuest</c>/Quest constructor reads is already frozen (and, if per-client-divergent,
/// captured+forced) at CREATION time - so a bare, uncorrected replay of <c>IssueManager.StartIssueQuest</c> on
/// every other peer reconstructs a byte-identical Quest object, exactly like
/// <c>VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue</c> already relies on (see
/// <see cref="VillageNeedsToolsIssueInterface"/>'s own type doc comment for the full derivation). Verified per
/// type against the decompiled source before being added here - see each bespoke Interfaces/*IssueInterface.cs
/// file's own doc comment for the types NOT in this set and why they need their own accept-time force-write
/// instead.
///
/// A type belongs in <see cref="AlternativeSolutionMirrorEligible"/> only if it has
/// <c>IsThereAlternativeSolution == true</c> and its alternative-solution path needs no additional
/// per-client-divergent field beyond what <c>MirrorAlternativeAccepted</c> already forces (issue state +
/// <c>IsTriedToSolveBefore</c> + due time) - true for every type here, since none of them stash an
/// alternative-solution-specific payment/amount the way a hypothetical future type might.
/// </summary>
internal static class GenericAcceptMirrorIssueTypes
{
    internal static readonly HashSet<Type> QuestSolutionMirrorEligible = new HashSet<Type>
    {
        typeof(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue),
        typeof(LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue),
        typeof(CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue),
        typeof(ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssue),
        typeof(ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue),
    };

    internal static readonly HashSet<Type> AlternativeSolutionMirrorEligible = new HashSet<Type>
    {
        typeof(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue),
        typeof(LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue),
        typeof(CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue),
        typeof(LandlordTrainingForRetainersIssueBehavior.LandlordTrainingForRetainersIssue),
        typeof(GangLeaderNeedsRecruitsIssueBehavior.GangLeaderNeedsRecruitsIssue),
        // Tier 1 Group 1B additions - all three have IsThereAlternativeSolution == true and need no
        // additional per-client-divergent field beyond what MirrorAlternativeAccepted already forces.
        // LordNeedsGarrisonTroopsIssue has AlternativeSolutionScaleFlag.FailureRisk (like
        // CapturedByBountyHuntersIssue above) - see NewIssueTypesAlternativeSolutionCompletionPatches'
        // TryTriggerOwnedAlternativeSolutionCompletion doc comment for why that's still safe to route through
        // this same generic trigger.
        typeof(LandLordNeedsManualLaborersIssueBehavior.LandLordNeedsManualLaborersIssue),
        typeof(HeadmanVillageNeedsDraughtAnimalsIssueBehavior.HeadmanVillageNeedsDraughtAnimalsIssue),
        typeof(LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssue),
    };

    internal static bool IsQuestSolutionMirrorEligible(IssueBase issue) =>
        issue != null && QuestSolutionMirrorEligible.Contains(issue.GetType());

    internal static bool IsAlternativeSolutionMirrorEligible(IssueBase issue) =>
        issue != null && AlternativeSolutionMirrorEligible.Contains(issue.GetType());
}
