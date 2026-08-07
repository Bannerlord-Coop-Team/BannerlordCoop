using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Interfaces;

internal static class GenericAcceptMirrorIssueTypes
{
    internal static readonly HashSet<Type> QuestSolutionMirrorEligible = new HashSet<Type>
    {
        typeof(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue),
        typeof(LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue),
        typeof(CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue),
        typeof(ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssue),
        typeof(ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue),
        // TheSpyPartyIssue is deliberately NOT here - its GenerateIssueQuest rolls the spy identity itself at
        // accept time (see ITheSpyPartyIssueInterface).
        typeof(NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssue),
        typeof(LandLordTheArtOfTheTradeIssueBehavior.LandLordTheArtOfTheTradeIssue),
        typeof(SandBox.Issues.RuralNotableInnAndOutIssueBehavior.RuralNotableInnAndOutIssue),
        typeof(SandBox.Issues.ProdigalSonIssueBehavior.ProdigalSonIssue),
        typeof(HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssue),
        typeof(HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue),
        typeof(ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue),
        // Gang Leader Needs to Offload Stolen Goods is deliberately NOT here - its GenerateIssueQuest re-derives
        // price/reward from live state at accept time (see IGangLeaderNeedsToOffloadStolenGoodsIssueInterface).
        typeof(SmugglersIssueBehavior.SmugglersIssue),
        typeof(ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue),
        typeof(LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssue),
        typeof(CaravanAmbushIssueBehavior.CaravanAmbushIssue),
        typeof(GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue),
        typeof(MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssue),
        typeof(LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssue),
        typeof(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue),
        typeof(TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue),
        typeof(RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryIssue),
        typeof(SandBox.Issues.RivalGangMovingInIssueBehavior.RivalGangMovingInIssue),
        typeof(LordWantsRivalCapturedIssueBehavior.LordWantsRivalCapturedIssue),
        // Snare the Wealthy is deliberately NOT here - GenerateIssueQuest rolls a genuine random target
        // settlement (see Generic.Migrated.SnareTheWealthy.SnareTheWealthyQuestType). IS in AlternativeSolutionMirrorEligible below.
    };

    internal static readonly HashSet<Type> AlternativeSolutionMirrorEligible = new HashSet<Type>
    {
        typeof(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue),
        typeof(LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue),
        typeof(CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue),
        typeof(LandlordTrainingForRetainersIssueBehavior.LandlordTrainingForRetainersIssue),
        typeof(GangLeaderNeedsRecruitsIssueBehavior.GangLeaderNeedsRecruitsIssue),
        typeof(LandLordNeedsManualLaborersIssueBehavior.LandLordNeedsManualLaborersIssue),
        typeof(HeadmanVillageNeedsDraughtAnimalsIssueBehavior.HeadmanVillageNeedsDraughtAnimalsIssue),
        typeof(LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssue),
        typeof(NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssue),
        typeof(LandLordTheArtOfTheTradeIssueBehavior.LandLordTheArtOfTheTradeIssue),
        typeof(SandBox.Issues.RuralNotableInnAndOutIssueBehavior.RuralNotableInnAndOutIssue),
        typeof(SandBox.Issues.ProdigalSonIssueBehavior.ProdigalSonIssue),
        typeof(SandBox.Issues.TheSpyPartyIssueQuestBehavior.TheSpyPartyIssue),
        typeof(HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssue),
        typeof(HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue),
        typeof(ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue),
        // GangLeaderNeedsToOffloadStolenGoods is deliberately NOT here - it graduated to its own bespoke
        // Generic-shape accept mirror (Generic.Migrated.GangLeaderNeedsToOffloadStolenGoods.
        // GangLeaderNeedsToOffloadStolenGoodsQuestType), which captures more than this shared mirror forces
        // (failure chance/casualty count/companion reward skill/troop XP, not just state+due time - see
        // AlternativeSolutionVanillaState), and has its own dedicated completion trigger/ownership gate instead
        // of this file's shared HourlyTick scan.
        typeof(SmugglersIssueBehavior.SmugglersIssue),
        typeof(ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue),
        typeof(CaravanAmbushIssueBehavior.CaravanAmbushIssue),
        typeof(GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue),
        typeof(MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssue),
        typeof(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue),
        typeof(SandBox.Issues.RivalGangMovingInIssueBehavior.RivalGangMovingInIssue),
        typeof(SandBox.Issues.SnareTheWealthyIssueBehavior.SnareTheWealthyIssue),
    };

    internal static bool IsQuestSolutionMirrorEligible(IssueBase issue) =>
        issue != null && QuestSolutionMirrorEligible.Contains(issue.GetType());

    internal static bool IsAlternativeSolutionMirrorEligible(IssueBase issue) =>
        issue != null && AlternativeSolutionMirrorEligible.Contains(issue.GetType());
}
