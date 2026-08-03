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
        // Tier 1 Group 1C/1D additions - GenerateIssueQuest is deterministic for all four given what's already
        // captured/forced at creation time (see each type's own bespoke Interfaces/*IssueInterface.cs, or
        // SimpleIssueFactoryRegistry's doc comment for Art of the Trade/Rural Notable). TheSpyPartyIssue is
        // deliberately NOT here - its GenerateIssueQuest rolls the load-bearing spy identity itself, at accept
        // time, and needs its own bespoke capture instead (see ITheSpyPartyIssueInterface's doc comment).
        typeof(NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssue),
        typeof(LandLordTheArtOfTheTradeIssueBehavior.LandLordTheArtOfTheTradeIssue),
        typeof(SandBox.Issues.RuralNotableInnAndOutIssueBehavior.RuralNotableInnAndOutIssue),
        typeof(SandBox.Issues.ProdigalSonIssueBehavior.ProdigalSonIssue),
        // Village Needs Grain Seeds: GenerateIssueQuest(questId) just forwards IssueOwner/IssueDifficultyMultiplier/
        // RewardGold(0)/NeededGrainAmount, all already frozen/derivable by accept time (see
        // SimpleIssueFactoryRegistry's doc comment) - a bare replay of IssueManager.StartIssueQuest lands on a
        // byte-identical HeadmanNeedsGrainIssueQuest on every peer.
        typeof(HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssue),
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
        // Tier 1 Group 1C/1D additions - all five have IsThereAlternativeSolution == true and need no
        // additional per-client-divergent field beyond what MirrorAlternativeAccepted already forces. Nearby
        // Bandit Base/Rural Notable/Prodigal Son/The Spy Party all have AlternativeSolutionScaleFlag.FailureRisk
        // (like CapturedByBountyHuntersIssue above) - see NewIssueTypesAlternativeSolutionCompletionPatches'
        // TryTriggerOwnedAlternativeSolutionCompletion doc comment for why that's still safe to route through
        // this same generic trigger. TheSpyPartyIssue IS included here even though it's NOT in
        // QuestSolutionMirrorEligible above - its alternative-solution path never reads the accept-time
        // selected-spy roll at all (AlternativeSolutionEndWithSuccess/FailureConsequence only touch IssueBase-
        // level relation/renown/town state), so it has no analogous divergence to fix on this path.
        typeof(NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssue),
        typeof(LandLordTheArtOfTheTradeIssueBehavior.LandLordTheArtOfTheTradeIssue),
        typeof(SandBox.Issues.RuralNotableInnAndOutIssueBehavior.RuralNotableInnAndOutIssue),
        typeof(SandBox.Issues.ProdigalSonIssueBehavior.ProdigalSonIssue),
        typeof(SandBox.Issues.TheSpyPartyIssueQuestBehavior.TheSpyPartyIssue),
        // Village Needs Grain Seeds: IsThereAlternativeSolution == true, AlternativeSolutionScaleFlags is
        // Duration only (no FailureRisk - confirmed against the decompiled source), so
        // MirrorAlternativeAccepted's generic force-write (issue state + IsTriedToSolveBefore + due time) is
        // all any other peer's mirror copy needs; the real per-accepter-only consequence
        // (AlternativeSolutionEndWithSuccessConsequence) is triggered exclusively through the same shared,
        // ownership-gated HourlyTick mechanism every other Tier 1 Group 1C/1D type uses - see
        // NewIssueTypesAlternativeSolutionPatches's Village Needs Grain Seeds registration.
        typeof(HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssue),
    };

    internal static bool IsQuestSolutionMirrorEligible(IssueBase issue) =>
        issue != null && QuestSolutionMirrorEligible.Contains(issue.GetType());

    internal static bool IsAlternativeSolutionMirrorEligible(IssueBase issue) =>
        issue != null && AlternativeSolutionMirrorEligible.Contains(issue.GetType());
}
