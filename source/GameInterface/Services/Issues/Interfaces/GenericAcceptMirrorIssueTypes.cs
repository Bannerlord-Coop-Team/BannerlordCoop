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
        // Deliver the Herd to Town: GenerateIssueQuest(questId) forwards IssueOwner/AnimalCountToDeliver/
        // _herdTypeToDeliver/_targetSettlement/RewardGold/_targetHero - all already frozen (three chained
        // rolls, forced at creation - see IHeadmanNeedsToDeliverAHerdIssueInterface's doc comment) or pure
        // functions of those frozen fields plus the same deterministic IssueDifficultyMultiplier every other
        // type here already relies on. A bare replay of IssueManager.StartIssueQuest lands on a
        // byte-identical HeadmanNeedsToDeliverAHerdIssueQuest on every peer.
        typeof(HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue),
        // Tier 2 Group B: Artisan Can't Sell Products At A Fair Price - GenerateIssueQuest(questId) forwards
        // IssueOwner/CampaignTime.DaysFromNow(18f)/_targetSettlement/_rawMaterialsToBeDelivered/
        // RawMaterialCountToBeDelivered/RewardGold/_targetHero/CounterOfferHero - all already frozen (four
        // independent rolls, forced at creation - see IArtisanCantSellProductsAtAFairPriceIssueInterface's doc
        // comment) or pure functions of those frozen fields plus the same deterministic IssueDifficultyMultiplier
        // every other type here already relies on. A bare replay of IssueManager.StartIssueQuest lands on a
        // byte-identical ArtisanCantSellProductsAtAFairPriceIssueQuest on every peer. Gang Leader Needs to
        // Offload Stolen Goods is deliberately NOT here - its GenerateIssueQuest re-derives
        // StolenTradeGoodAmount/StolenTradeGoodPrice/RewardGold from live Town.GetItemPrice AND
        // IssueDifficultyMultiplier at accept time, both genuinely per-client-divergent - needs its own bespoke
        // capture instead (see IGangLeaderNeedsToOffloadStolenGoodsIssueInterface's doc comment).
        typeof(ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue),
        // Tier 2 Group A: Smugglers - GenerateIssueQuest(questId) forwards IssueOwner/_targetSettlement/
        // _originSettlement/base.IssueDifficultyMultiplier/CampaignTime.DaysFromNow(20f)/RewardGold. The two
        // settlements are frozen (and forced) at creation time - see ISmugglersIssueInterface's doc comment -
        // and RewardGold/IssueDifficultyMultiplier are the same deterministic Campaign.Current.PlayerProgress-
        // derived value every other type here already relies on (confirmed against the decompiled
        // DefaultIssueModel.GetIssueDifficultyMultiplier - a pure function of a single shared Campaign-level
        // value, not Hero/party-relative, so every peer's own independent replay lands on the same number). A
        // bare replay of IssueManager.StartIssueQuest lands on a byte-identical SmugglersIssueQuest on every
        // peer. The one genuinely per-client-divergent piece of this quest - _smugglerParty, only ever set
        // later by the dialogue-triggered QuestAcceptedConsequences, not by this replay - is handled entirely
        // separately by SmugglersPartySpawnGatePatch, not by this generic accept mirror.
        typeof(SmugglersIssueBehavior.SmugglersIssue),
        // Tier 2 Group B (continued): Artisan Overpriced Goods - GenerateIssueQuest(questId) forwards
        // IssueOwner/CampaignTime.DaysFromNow(30f)/_requestedTradeGood/RewardGold/RequestedTradeGoodAmount/
        // CounterOfferHero. The Issue ctor takes _requestedTradeGood/CounterOfferHero DIRECTLY (both picked
        // server-side in ConditionsHold/GetAntagonistMerchant before CreateNewIssue ever runs - see
        // IArtisanOverpricedGoodsIssueInterface's doc comment), and RewardGold/RequestedTradeGoodAmount are
        // pure functions of _requestedTradeGood.Value and the same deterministic IssueDifficultyMultiplier
        // every other type here already relies on (CalculateTradeGoodsAmountAndReward, run unconditionally
        // inside the ctor) - so once creation is captured/forced, a bare replay of IssueManager.StartIssueQuest
        // lands on a byte-identical ArtisanOverpricedGoodsIssueQuest on every peer. Contrary to this task's
        // initial assumption (verified against the decompiled source rather than trusted, per this session's
        // established pattern), this type does NOT need its own bespoke accept-time capture for these scalar
        // fields - the one genuinely bespoke mechanic it needs (the AntagonistHero/CounterOfferHero identity
        // fix) is a pure, local, network-message-free Harmony patch entirely orthogonal to accept-time mirror
        // eligibility - see Patches.ArtisanOverpricedGoodsAntagonistIdentityPatches.
        typeof(ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue),
        // Lord Needs a Tutor - GenerateIssueQuest(questId) forwards IssueOwner/a fixed 200-day duration/
        // _youngHero - all already frozen at creation (_youngHero is captured/forced the same "direct ctor arg"
        // way as ArtisanOverpricedGoodsIssue/RevenueFarmingIssue - see ILordsNeedsTutorIssueInterface's doc
        // comment). The Quest ctor itself DOES roll one fresh value, _randomForQuestReward = MBRandom.RandomInt
        // (2, 5) - genuinely per-client-divergent if bare-replayed - but this is safe: it's read in exactly one
        // place (QuestCompletedWithSuccessAfterConversation, the jewelry-reward count), and that method is
        // gated to the recorded owner only (see Patches.LordsNeedsTutorOwnershipGatePatches), so no other
        // peer's own independently-rolled copy is ever consulted or acted upon, and it never surfaces in any
        // Title/Description/journal text (confirmed against the decompiled source) - see
        // ILordsNeedsTutorIssueInterface's doc comment for the full derivation.
        typeof(LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssue),
        // Tier 2 Group A: Caravan Ambush - GenerateIssueQuest(questId) forwards "caravan_ambush_quest_" + a
        // timestamp string/IssueOwner/_targetSettlement/CampaignTime.DaysFromNow(20f)/RewardGold/
        // IssueDifficultyMultiplier. _targetSettlement is frozen (and forced) at creation time - see
        // ICaravanAmbushIssueInterface's doc comment - and RewardGold/IssueDifficultyMultiplier are the same
        // deterministic Campaign.Current.PlayerProgress-derived value every other type here already relies on.
        // The questId's own timestamp component (CampaignTime.Now.ElapsedSecondsUntilNow) is purely a string
        // identifier, never read back by any game logic, and not itself a source of divergence risk since
        // QuestBase's own StringId assignment (via IssueManager.StartIssueQuest's shared bookkeeping) is what
        // actually needs to match across peers - confirmed unaffected. A bare replay of
        // IssueManager.StartIssueQuest lands on a byte-identical CaravanAmbushIssueQuest on every peer. The two
        // genuinely per-client-divergent pieces of this quest - _caravanParty/_banditParty/_rewardItems, only
        // ever set later by the dialogue-triggered OnQuestAccepted - are handled entirely separately by
        // CaravanAmbushPartySpawnGatePatch, not by this generic accept mirror.
        typeof(CaravanAmbushIssueBehavior.CaravanAmbushIssue),
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
        // Deliver the Herd to Town: IsThereAlternativeSolution == true, AlternativeSolutionScaleFlags is
        // Duration only (confirmed against the decompiled source - no FailureRisk/Casualties), so
        // MirrorAlternativeAccepted's generic force-write is all any other peer's mirror copy needs.
        typeof(HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue),
        // Tier 2 Group B: Artisan Can't Sell Products At A Fair Price - IsThereAlternativeSolution == true,
        // AlternativeSolutionScaleFlags is Duration only (confirmed against the decompiled source - no
        // FailureRisk/Casualties), so MirrorAlternativeAccepted's generic force-write is all any other peer's
        // mirror copy needs. Gang Leader Needs to Offload Stolen Goods IS included here too, even though it's
        // NOT in QuestSolutionMirrorEligible above (its GenerateIssueQuest needs bespoke capture, but its
        // alternative-solution path's own AlternativeSolutionEndWithSuccessConsequence is always gated to the
        // recorded owner - see GangLeaderNeedsToOffloadStolenGoodsOwnershipGatePatches - so the generic issue-
        // state force-write here is exactly what every OTHER (non-owning) peer's mirror copy needs);
        // AlternativeSolutionScaleFlags for it is the base IssueBase default (None - confirmed against the
        // decompiled source, not overridden), so no FailureRisk either.
        typeof(ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue),
        typeof(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue),
        // Tier 2 Group A: Smugglers - IsThereAlternativeSolution == true, AlternativeSolutionScaleFlags is
        // Casualties | FailureRisk (confirmed against the decompiled source) - the same shape as
        // CapturedByBountyHuntersIssue above, which genuinely can fail and is still safe to route through this
        // same generic trigger (see NewIssueTypesAlternativeSolutionCompletionPatches' doc comment). Needs its
        // matching HourlyTick registration too - see that file's SmugglersIssueBehavior entry.
        typeof(SmugglersIssueBehavior.SmugglersIssue),
        // Artisan Overpriced Goods - IsThereAlternativeSolution == true, AlternativeSolutionScaleFlags is
        // Duration only (confirmed against the decompiled source - no FailureRisk/Casualties), so
        // MirrorAlternativeAccepted's generic force-write (issue state + IsTriedToSolveBefore + due time) is
        // all any other peer's mirror copy needs.
        typeof(ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue),
        // Tier 2 Group A: Caravan Ambush - IsThereAlternativeSolution == true, AlternativeSolutionScaleFlags is
        // Casualties | FailureRisk (confirmed against the decompiled source) - the same shape as
        // CapturedByBountyHuntersIssue/SmugglersIssue above, genuinely can fail and still safe to route through
        // this same generic trigger. Needs its matching HourlyTick registration too - see that file's
        // CaravanAmbushIssueBehavior entry.
        typeof(CaravanAmbushIssueBehavior.CaravanAmbushIssue),
    };

    internal static bool IsQuestSolutionMirrorEligible(IssueBase issue) =>
        issue != null && QuestSolutionMirrorEligible.Contains(issue.GetType());

    internal static bool IsAlternativeSolutionMirrorEligible(IssueBase issue) =>
        issue != null && AlternativeSolutionMirrorEligible.Contains(issue.GetType());
}
