using SandBox.Issues;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues.Commands;

// One entry per real vanilla IssueBase subtype (43 total). A "not wired" entry has a null Resolve and a
// non-null NotWiredReason, which IssuesDebugCommand reports verbatim rather than silently no-op'ing.
internal static class IssueGiveCatalog
{
    private static readonly Dictionary<string, IssueGiveEntry> ByKey = Build();

    public static IReadOnlyDictionary<string, IssueGiveEntry> Entries => ByKey;

    public static bool TryGet(string key, out IssueGiveEntry entry) => ByKey.TryGetValue(key ?? string.Empty, out entry);

    private static (PotentialIssueData.StartIssueDelegate Factory, string Error) Ok(Func<Hero, IssueBase> build)
        => (new PotentialIssueData.StartIssueDelegate((in PotentialIssueData _, Hero owner) => build(owner)), null);

    private static (PotentialIssueData.StartIssueDelegate Factory, string Error) Fail(string reason)
        => (null, reason);

    // Debug "give" bypasses each type's own eligibility selection - these are simple always-safe defaults.

    private static Settlement AnyFortification(Hero hero)
    {
        var fortifications = Settlement.All.Where(s => s.IsTown || s.IsCastle).ToList();
        if (fortifications.Count == 0) return null;

        var faction = hero?.MapFaction;
        if (faction != null)
        {
            var hostile = fortifications.FirstOrDefault(s => s.MapFaction != null && s.MapFaction.IsAtWarWith(faction));
            if (hostile != null) return hostile;
        }

        return fortifications.FirstOrDefault(s => s.MapFaction != faction) ?? fortifications[0];
    }

    private static Settlement AnyHideout() => Settlement.All.FirstOrDefault(s => s.IsHideout);

    private static Settlement AnyVillageSettlement() => Settlement.All.FirstOrDefault(s => s.IsVillage);

    private static Hero AnyOtherHero(Hero hero) =>
        Hero.AllAliveHeroes.FirstOrDefault(h => h != hero && h.IsLord) ??
        Hero.AllAliveHeroes.FirstOrDefault(h => h != hero);

    private static List<Settlement> ThreeDistinctFortifications() =>
        Settlement.All.Where(s => s.IsTown || s.IsCastle).Take(3).ToList();

    private static (Settlement First, Settlement Second)? TwoDistinctTowns()
    {
        var towns = Settlement.All.Where(s => s.IsTown).Take(2).ToList();
        return towns.Count == 2 ? (towns[0], towns[1]) : ((Settlement, Settlement)?)null;
    }

    // --- Catalog -------------------------------------------------------------------------------------------

    private static void Wire(
        Dictionary<string, IssueGiveEntry> map, string key, Type issueType,
        Func<Hero, (PotentialIssueData.StartIssueDelegate, string)> resolve) =>
        map[key] = new IssueGiveEntry(key, issueType, resolve, null);

    private static void NotWired(Dictionary<string, IssueGiveEntry> map, string key, Type issueType, string reason) =>
        map[key] = new IssueGiveEntry(key, issueType, null, reason);

    private static Dictionary<string, IssueGiveEntry> Build()
    {
        var map = new Dictionary<string, IssueGiveEntry>(StringComparer.OrdinalIgnoreCase);
        AddBareHeroEntries(map);
        AddRelatedObjectEntries(map);
        AddNotYetWiredEntries(map);
        return map;
    }

    private static void AddBareHeroEntries(Dictionary<string, IssueGiveEntry> map)
    {
        void Wire(string key, Type issueType, Func<Hero, (PotentialIssueData.StartIssueDelegate, string)> resolve) =>
            IssueGiveCatalog.Wire(map, key, issueType, resolve);

        Wire("ArmyNeedsSupplies", typeof(ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssue),
            hero => Ok(h => new ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssue(h)));

        Wire("ArtisanCantSellProductsAtAFairPrice", typeof(ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue),
            hero => Ok(h => new ArtisanCantSellProductsAtAFairPriceIssueBehavior.ArtisanCantSellProductsAtAFairPriceIssue(h)));

        Wire("BettingFraud", typeof(BettingFraudIssueBehavior.BettingFraudIssue),
            hero => Ok(h => new BettingFraudIssueBehavior.BettingFraudIssue(h)));

        Wire("EscortMerchantCaravan", typeof(EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue),
            hero => Ok(h => new EscortMerchantCaravanIssueBehavior.EscortMerchantCaravanIssue(h)));

        Wire("ExtortionByDeserters", typeof(ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssue),
            hero => Ok(h => new ExtortionByDesertersIssueBehavior.ExtortionByDesertersIssue(h)));

        Wire("GangLeaderNeedsRecruits", typeof(GangLeaderNeedsRecruitsIssueBehavior.GangLeaderNeedsRecruitsIssue),
            hero => Ok(h => new GangLeaderNeedsRecruitsIssueBehavior.GangLeaderNeedsRecruitsIssue(h)));

        Wire("GangLeaderNeedsSpecialWeapons", typeof(GangLeaderNeedsSpecialWeaponsIssueBehavior.GangLeaderNeedsSpecialWeaponsIssue),
            hero => Ok(h => new GangLeaderNeedsSpecialWeaponsIssueBehavior.GangLeaderNeedsSpecialWeaponsIssue(h)));

        Wire("GangLeaderNeedsWeapons", typeof(GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue),
            hero => Ok(h => new GangLeaderNeedsWeaponsIssueQuestBehavior.GangLeaderNeedsWeaponsIssue(h)));

        Wire("HeadmanNeedsGrain", typeof(HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssue),
            hero => Ok(h => new HeadmanNeedsGrainIssueBehavior.HeadmanNeedsGrainIssue(h)));

        Wire("HeadmanNeedsToDeliverAHerd", typeof(HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue),
            hero => Ok(h => new HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue(h)));

        Wire("HeadmanVillageNeedsDraughtAnimals", typeof(HeadmanVillageNeedsDraughtAnimalsIssueBehavior.HeadmanVillageNeedsDraughtAnimalsIssue),
            hero => Ok(h => new HeadmanVillageNeedsDraughtAnimalsIssueBehavior.HeadmanVillageNeedsDraughtAnimalsIssue(h)));

        Wire("LadysKnightOut", typeof(LadysKnightOutIssueBehavior.LadysKnightOutIssue),
            hero => Ok(h => new LadysKnightOutIssueBehavior.LadysKnightOutIssue(h)));

        Wire("LandLordCompanyOfTrouble", typeof(LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssue),
            hero => Ok(h => new LandLordCompanyOfTroubleIssueBehavior.LandLordCompanyOfTroubleIssue(h)));

        Wire("LandlordNeedsAccessToVillageCommons", typeof(LandlordNeedsAccessToVillageCommonsIssueBehavior.LandlordNeedsAccessToVillageCommonsIssue),
            hero => Ok(h => new LandlordNeedsAccessToVillageCommonsIssueBehavior.LandlordNeedsAccessToVillageCommonsIssue(h)));

        Wire("LandLordNeedsManualLaborers", typeof(LandLordNeedsManualLaborersIssueBehavior.LandLordNeedsManualLaborersIssue),
            hero => Ok(h => new LandLordNeedsManualLaborersIssueBehavior.LandLordNeedsManualLaborersIssue(h)));

        Wire("LandlordTrainingForRetainers", typeof(LandlordTrainingForRetainersIssueBehavior.LandlordTrainingForRetainersIssue),
            hero => Ok(h => new LandlordTrainingForRetainersIssueBehavior.LandlordTrainingForRetainersIssue(h)));

        Wire("LesserNobleRevolt", typeof(LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssue),
            hero => Ok(h => new LesserNobleRevoltIssueBehavior.LesserNobleRevoltIssue(h)));

        Wire("LordNeedsHorses", typeof(LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue),
            hero => Ok(h => new LordNeedsHorsesIssueBehavior.LordNeedsHorsesIssue(h)));

        Wire("RaidAnEnemyTerritory", typeof(RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryIssue),
            hero => Ok(h => new RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryIssue(h)));

        Wire("VillageNeedsCraftingMaterials", typeof(VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue),
            hero => Ok(h => new VillageNeedsCraftingMaterialsIssueBehavior.VillageNeedsCraftingMaterialsIssue(h)));

        // The hero-minting side effect this project flagged elsewhere lives in the Quest ctor, not this Issue
        // ctor - not a special risk from this debug command.
        Wire("NotableWantsDaughterFound", typeof(NotableWantsDaughterFoundIssueBehavior.NotableWantsDaughterFoundIssue),
            hero => Ok(h => new NotableWantsDaughterFoundIssueBehavior.NotableWantsDaughterFoundIssue(h)));

        Wire("RuralNotableInnAndOut", typeof(RuralNotableInnAndOutIssueBehavior.RuralNotableInnAndOutIssue),
            hero => Ok(h => new RuralNotableInnAndOutIssueBehavior.RuralNotableInnAndOutIssue(h)));
    }

    private static void AddRelatedObjectEntries(Dictionary<string, IssueGiveEntry> map)
    {
        AddFortificationRelatedEntries(map);
        AddHideoutRelatedEntries(map);
        AddOtherHeroRelatedEntries(map);
        AddMiscRelatedEntries(map);
    }

    private static void AddFortificationRelatedEntries(Dictionary<string, IssueGiveEntry> map)
    {
        void Wire(string key, Type issueType, Func<Hero, (PotentialIssueData.StartIssueDelegate, string)> resolve) =>
            IssueGiveCatalog.Wire(map, key, issueType, resolve);

        Wire("TheConquestOfSettlement", typeof(TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue), hero =>
        {
            var settlement = AnyFortification(hero);
            return settlement == null
                ? Fail("no town/castle settlement exists in the current campaign")
                : Ok(h => new TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue(h, settlement));
        });

        Wire("CaravanAmbush", typeof(CaravanAmbushIssueBehavior.CaravanAmbushIssue), hero =>
        {
            var settlement = AnyFortification(hero);
            return settlement == null
                ? Fail("no town/castle settlement exists in the current campaign")
                : Ok(h => new CaravanAmbushIssueBehavior.CaravanAmbushIssue(h, settlement));
        });

        Wire("RevenueFarming", typeof(RevenueFarmingIssueBehavior.RevenueFarmingIssue), hero =>
        {
            var settlement = AnyFortification(hero);
            return settlement == null
                ? Fail("no town/castle settlement exists in the current campaign")
                : Ok(h => new RevenueFarmingIssueBehavior.RevenueFarmingIssue(h, settlement));
        });

        Wire("LordNeedsGarrisonTroops", typeof(LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssue), hero =>
        {
            var settlement = AnyFortification(hero);
            return settlement == null
                ? Fail("no town/castle settlement exists in the current campaign")
                : Ok(h => new LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssue(h, settlement));
        });

        Wire("TheSpyParty", typeof(TheSpyPartyIssueQuestBehavior.TheSpyPartyIssue), hero =>
        {
            var settlement = AnyFortification(hero);
            return settlement == null
                ? Fail("no town/castle settlement exists in the current campaign")
                : Ok(h => new TheSpyPartyIssueQuestBehavior.TheSpyPartyIssue(h, settlement));
        });
    }

    private static void AddHideoutRelatedEntries(Dictionary<string, IssueGiveEntry> map)
    {
        void Wire(string key, Type issueType, Func<Hero, (PotentialIssueData.StartIssueDelegate, string)> resolve) =>
            IssueGiveCatalog.Wire(map, key, issueType, resolve);

        Wire("CapturedByBountyHunters", typeof(CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue), hero =>
        {
            var hideout = AnyHideout();
            return hideout == null
                ? Fail("no hideout settlement exists in the current campaign")
                : Ok(h => new CapturedByBountyHuntersIssueBehavior.CapturedByBountyHuntersIssue(h, hideout));
        });

        Wire("NearbyBanditBase", typeof(NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssue), hero =>
        {
            var hideout = AnyHideout();
            return hideout == null
                ? Fail("no hideout settlement exists in the current campaign")
                : Ok(h => new NearbyBanditBaseIssueBehavior.NearbyBanditBaseIssue(h, hideout));
        });

        Wire("GangLeaderNeedsToOffloadStolenGoods", typeof(GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue), hero =>
        {
            var hideout = AnyHideout();
            return hideout == null
                ? Fail("no hideout settlement exists in the current campaign")
                : Ok(h => new GangLeaderNeedsToOffloadStolenGoodsIssueBehavior.GangLeaderNeedsToOffloadStolenGoodsIssue(h, hideout));
        });

        Wire("MerchantNeedsHelpWithOutlaws", typeof(MerchantNeedsHelpWithOutlawsIssueQuestBehavior.MerchantNeedsHelpWithOutlawsIssue), hero =>
        {
            var hideoutSettlement = AnyHideout();
            return hideoutSettlement?.Hideout == null
                ? Fail("no hideout settlement exists in the current campaign")
                : Ok(h => new MerchantNeedsHelpWithOutlawsIssueQuestBehavior.MerchantNeedsHelpWithOutlawsIssue(h, hideoutSettlement.Hideout));
        });
    }

    private static void AddOtherHeroRelatedEntries(Dictionary<string, IssueGiveEntry> map)
    {
        void Wire(string key, Type issueType, Func<Hero, (PotentialIssueData.StartIssueDelegate, string)> resolve) =>
            IssueGiveCatalog.Wire(map, key, issueType, resolve);

        Wire("LordsNeedsTutor", typeof(LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssue), hero =>
        {
            var youngHero = AnyOtherHero(hero);
            return youngHero == null
                ? Fail("no other alive hero exists in the current campaign to act as the young hero")
                : Ok(h => new LordsNeedsTutorIssueBehavior.LordsNeedsTutorIssue(h, youngHero));
        });

        Wire("LordWantsRivalCaptured", typeof(LordWantsRivalCapturedIssueBehavior.LordWantsRivalCapturedIssue), hero =>
        {
            var target = AnyOtherHero(hero);
            return target == null
                ? Fail("no other alive hero exists in the current campaign to act as the target hero")
                : Ok(h => new LordWantsRivalCapturedIssueBehavior.LordWantsRivalCapturedIssue(h, target));
        });

        Wire("RivalGangMovingIn", typeof(RivalGangMovingInIssueBehavior.RivalGangMovingInIssue), hero =>
        {
            var rival = AnyOtherHero(hero);
            return rival == null
                ? Fail("no other alive hero exists in the current campaign to act as the rival gang leader")
                : Ok(h => new RivalGangMovingInIssueBehavior.RivalGangMovingInIssue(h, rival));
        });

        Wire("SnareTheWealthy", typeof(SnareTheWealthyIssueBehavior.SnareTheWealthyIssue), hero =>
        {
            var merchant = AnyOtherHero(hero);
            return merchant == null
                ? Fail("no other alive hero exists in the current campaign to act as the target merchant")
                : Ok(h => new SnareTheWealthyIssueBehavior.SnareTheWealthyIssue(h, merchant.CharacterObject));
        });

        Wire("ArtisanOverpricedGoods", typeof(ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue), hero =>
        {
            var counterOfferHero = AnyOtherHero(hero);
            return counterOfferHero == null
                ? Fail("no other alive hero exists in the current campaign to act as the counter-offer hero")
                : Ok(h => new ArtisanOverpricedGoodsIssueBehavior.ArtisanOverpricedGoodsIssue(h, counterOfferHero, DefaultItems.Grain));
        });
    }

    private static void AddMiscRelatedEntries(Dictionary<string, IssueGiveEntry> map)
    {
        void Wire(string key, Type issueType, Func<Hero, (PotentialIssueData.StartIssueDelegate, string)> resolve) =>
            IssueGiveCatalog.Wire(map, key, issueType, resolve);

        Wire("VillageNeedsTools", typeof(VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue),
            hero => Ok(h => new VillageNeedsToolsIssueBehavior.VillageNeedsToolsIssue(h, DefaultItems.Tools)));

        Wire("MerchantArmyOfPoachers", typeof(MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssue), hero =>
        {
            var village = AnyVillageSettlement();
            return village?.Village == null
                ? Fail("no village settlement exists in the current campaign")
                : Ok(h => new MerchantArmyOfPoachersIssueBehavior.MerchantArmyOfPoachersIssue(h, village.Village));
        });

        Wire("LandLordTheArtOfTheTrade", typeof(LandLordTheArtOfTheTradeIssueBehavior.LandLordTheArtOfTheTradeIssue),
            hero => Ok(h => new LandLordTheArtOfTheTradeIssueBehavior.LandLordTheArtOfTheTradeIssue(h, DefaultItems.Grain)));

        Wire("ScoutEnemyGarrisons", typeof(ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue), hero =>
        {
            var settlements = ThreeDistinctFortifications();
            return settlements.Count < 3
                ? Fail("fewer than 3 town/castle settlements exist in the current campaign (the constructor indexes settlements[0..2])")
                : Ok(h => new ScoutEnemyGarrisonsIssueBehavior.ScoutEnemyGarrisonsIssue(h, settlements));
        });

        Wire("Smugglers", typeof(SmugglersIssueBehavior.SmugglersIssue), hero =>
        {
            var pair = TwoDistinctTowns();
            return pair == null
                ? Fail("fewer than 2 town settlements exist in the current campaign")
                : Ok(h => new SmugglersIssueBehavior.SmugglersIssue(h, new KeyValuePair<Settlement, Settlement>(pair.Value.First, pair.Value.Second)));
        });

        Wire("FamilyFeud", typeof(FamilyFeudIssueBehavior.FamilyFeudIssue), hero =>
        {
            var village = AnyVillageSettlement();
            if (village == null) return Fail("no village settlement exists in the current campaign");

            var notable = village.Notables.FirstOrDefault(n => n != hero) ?? AnyOtherHero(hero);
            return notable == null
                ? Fail("no other alive hero exists in the current campaign to act as the target notable")
                : Ok(h => new FamilyFeudIssueBehavior.FamilyFeudIssue(h, notable, village));
        });
    }

    private static void AddNotYetWiredEntries(Dictionary<string, IssueGiveEntry> map)
    {
        void NotWired(string key, Type issueType, string reason) =>
            IssueGiveCatalog.NotWired(map, key, issueType, reason);

        NotWired("ProdigalSon", typeof(ProdigalSonIssueBehavior.ProdigalSonIssue),
            "constructor reserves a location on the target hero's CURRENT settlement and unconditionally calls " +
            "DisableHeroAction.Apply on both the prodigal-son and target-gang heroes at issue-creation time " +
            "(not deferred to quest-accept like every other type) - requires the caller-supplied target hero to " +
            "currently be inside a settlement with a free reservable location or it null-refs, and even a " +
            "successful call silently disables an arbitrary chosen hero as a side effect. Too fragile to default " +
            "safely without guessing.");
    }
}

// One catalog row. A null Resolve means this type is a deliberate "known, not yet wired" gap.
internal sealed class IssueGiveEntry
{
    public string Key { get; }
    public Type IssueType { get; }
    public Func<Hero, (PotentialIssueData.StartIssueDelegate Factory, string Error)> Resolve { get; }
    public string NotWiredReason { get; }

    public IssueGiveEntry(
        string key,
        Type issueType,
        Func<Hero, (PotentialIssueData.StartIssueDelegate Factory, string Error)> resolve,
        string notWiredReason)
    {
        Key = key;
        IssueType = issueType;
        Resolve = resolve;
        NotWiredReason = notWiredReason;
    }
}
