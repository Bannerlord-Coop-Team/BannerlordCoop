using Common.Util;
using GameInterface.Services.Locations;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Tests.Bootstrap;
using Moq;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using Xunit;

namespace GameInterface.Tests.Services.Locations;

public class SettlementHeroSpawnPoolTests
{
    private readonly Mock<IObjectManager> objectManager = new();
    private readonly Mock<IPlayerManager> playerManager = new();

    public SettlementHeroSpawnPoolTests()
    {
        GameBootStrap.Initialize();
        playerManager.SetupGet(manager => manager.Players).Returns(Array.Empty<Player>());
    }

    [Fact]
    public void GetAmbientCandidates_HeroesWithoutParty_DeduplicatesAcrossSourcesAndSkipsNulls()
    {
        var locationComplex = new LocationComplex();
        Location location = CreateLocation("tavern", locationComplex);
        locationComplex._locations.Add(location.StringId, location);
        Settlement settlement = CreateSettlement(locationComplex);
        Hero hero = CreateHero("waiting-hero");
        settlement._heroesWithoutPartyCache.Add(hero);
        settlement._heroesWithoutPartyCache.Add(null);
        settlement._heroesWithoutPartyCache.Add(hero);
        location._characterList.Add(CreateLocationCharacter(hero));

        IReadOnlyCollection<Hero> candidates = CreatePool().GetAmbientCandidates(settlement);

        Assert.Same(hero, Assert.Single(candidates));
    }

    [Fact]
    public void GetAmbientCandidates_KingdomFactionLeaderAndSpouse_IncludesBoth()
    {
        Hero leader = CreateHero("faction-leader");
        Hero spouse = CreateHero("faction-spouse");
        leader.Spouse = spouse;

        var rulingClan = new Clan();
        rulingClan._leader = leader;
        leader._clan = rulingClan;
        spouse._clan = rulingClan;

        var kingdom = new Kingdom { RulingClan = rulingClan };
        var ownerClan = new Clan { _kingdom = kingdom };
        Settlement settlement = CreateFortification(ownerClan);

        IReadOnlyCollection<Hero> candidates = CreatePool().GetAmbientCandidates(settlement);

        Assert.Contains(leader, candidates);
        Assert.Contains(spouse, candidates);
    }

    [Fact]
    public void GetAmbientCandidates_AliveLords_IncludesLordsFromEveryRegisteredPlayersClan()
    {
        Hero firstPlayerHero = CreateHero("first-player");
        Hero firstLord = CreateHero("first-lord");
        Clan firstClan = CreateClan(firstPlayerHero, firstLord);
        firstPlayerHero._clan = firstClan;

        Hero secondPlayerHero = CreateHero("second-player");
        Hero secondLord = CreateHero("second-lord");
        Clan secondClan = CreateClan(secondPlayerHero, secondLord);
        secondPlayerHero._clan = secondClan;

        var firstPlayer = CreatePlayer("first", "first-hero-id");
        var secondPlayer = CreatePlayer("second", "second-hero-id");
        playerManager.SetupGet(manager => manager.Players).Returns(new[] { firstPlayer, secondPlayer });
        SetupPlayerHero(firstPlayer, firstPlayerHero);
        SetupPlayerHero(secondPlayer, secondPlayerHero);
        playerManager.Setup(manager => manager.Contains(firstPlayerHero)).Returns(true);
        playerManager.Setup(manager => manager.Contains(secondPlayerHero)).Returns(true);

        IReadOnlyCollection<Hero> candidates = CreatePool().GetAmbientCandidates(CreateSettlement());

        Assert.Contains(firstLord, candidates);
        Assert.Contains(secondLord, candidates);
        Assert.DoesNotContain(firstPlayerHero, candidates);
        Assert.DoesNotContain(secondPlayerHero, candidates);
    }

    [Fact]
    public void GetAmbientCandidates_PlayerPartyCompanions_ExcludesCompanionsUsingMissionMesh()
    {
        Hero playerHero = CreateHero("player");
        Hero companion = CreateHero("companion");
        var clan = new Clan();
        clan._companionsCache.Add(companion);
        playerHero._clan = clan;
        companion._companionOf = clan;

        MobileParty playerParty = CreateParty(playerHero);
        playerHero._partyBelongedTo = playerParty;
        companion._partyBelongedTo = playerParty;

        Player player = CreatePlayer("player", "player-hero-id");
        playerManager.SetupGet(manager => manager.Players).Returns(new[] { player });
        SetupPlayerHero(player, playerHero);
        playerManager.Setup(manager => manager.Contains(playerHero)).Returns(true);
        playerManager.Setup(manager => manager.Contains(playerParty)).Returns(true);

        IReadOnlyCollection<Hero> candidates = CreatePool().GetAmbientCandidates(CreateSettlement());

        Assert.DoesNotContain(companion, candidates);
    }

    [Fact]
    public void GetAmbientCandidates_PrisonerHeroes_IncludesSettlementPrisoners()
    {
        Settlement settlement = CreateFortification(new Clan());
        Hero prisoner = CreateHero("prisoner");
        settlement.Party.PrisonRoster.AddToCounts(prisoner.CharacterObject, 1);

        IReadOnlyCollection<Hero> candidates = CreatePool().GetAmbientCandidates(settlement);

        Assert.Contains(prisoner, candidates);
    }

    [Fact]
    public void GetAmbientCandidates_SettlementPartyLeaders_IncludesAiLeaderAndSkipsNullParty()
    {
        Settlement settlement = CreateSettlement();
        Hero aiLeader = CreateHero("ai-leader");
        MobileParty aiParty = CreateParty(aiLeader);
        aiLeader._partyBelongedTo = aiParty;
        settlement._partiesCache.Add(null);
        settlement._partiesCache.Add(aiParty);

        IReadOnlyCollection<Hero> candidates = CreatePool().GetAmbientCandidates(settlement);

        Assert.Contains(aiLeader, candidates);
    }

    [Fact]
    public void GetAmbientCandidates_ExistingLocationRoster_IncludesHeroEntry()
    {
        var locationComplex = new LocationComplex();
        Location location = CreateLocation("tavern", locationComplex);
        locationComplex._locations.Add(location.StringId, location);
        Settlement settlement = CreateSettlement(locationComplex);
        Hero existingHero = CreateHero("existing-roster-hero");
        location._characterList.Add(CreateLocationCharacter(existingHero));

        IReadOnlyCollection<Hero> candidates = CreatePool().GetAmbientCandidates(settlement);

        Assert.Contains(existingHero, candidates);
    }

    [Fact]
    public void GetAmbientCandidates_ControlledPlayerHero_ExcludesHeroFromAmbientSources()
    {
        Settlement settlement = CreateSettlement();
        Hero playerHero = CreateHero("controlled-player");
        settlement._heroesWithoutPartyCache.Add(playerHero);
        playerManager.Setup(manager => manager.Contains(playerHero)).Returns(true);

        IReadOnlyCollection<Hero> candidates = CreatePool().GetAmbientCandidates(settlement);

        Assert.Empty(candidates);
    }

    [Fact]
    public void GetAmbientCandidates_NullSettlementAndNullSourceCollections_ReturnEmpty()
    {
        Settlement settlement = CreateSettlement();
        settlement._heroesWithoutPartyCache = null;
        settlement._partiesCache = null;

        Assert.Empty(CreatePool().GetAmbientCandidates(null));
        Assert.Empty(CreatePool().GetAmbientCandidates(settlement));
    }

    private SettlementHeroSpawnPool CreatePool()
        => new(objectManager.Object, playerManager.Object);

    private void SetupPlayerHero(Player player, Hero hero)
    {
        Hero resolvedHero = hero;
        objectManager
            .Setup(manager => manager.TryGetObject(player.HeroId, out resolvedHero))
            .Returns(true);
    }

    private static Player CreatePlayer(string controllerId, string heroId)
        => new(controllerId, heroId, controllerId + "-party", controllerId + "-clan", controllerId + "-character");

    private static Clan CreateClan(params Hero[] aliveLords)
    {
        var clan = new Clan();
        foreach (Hero hero in aliveLords)
        {
            clan._aliveLordsCache.Add(hero);
            hero._clan = clan;
        }
        return clan;
    }

    private static Hero CreateHero(string id)
    {
        var hero = new Hero { StringId = id };
        var character = new CharacterObject
        {
            StringId = id + "-character",
            BodyPropertyRange = new MBBodyProperty(),
        };
        character.HeroObject = hero;
        hero._characterObject = character;
        return hero;
    }

    private static MobileParty CreateParty(Hero leader)
    {
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        var component = ObjectHelper.SkipConstructor<CustomPartyComponent>();
        component._leader = leader;
        party._partyComponent = component;
        return party;
    }

    private static Settlement CreateSettlement(LocationComplex locationComplex = null)
        => new(new TextObject("Test Settlement"), locationComplex, null);

    private static Settlement CreateFortification(Clan ownerClan)
    {
        Settlement settlement = CreateSettlement();
        var town = new Town { _ownerClan = ownerClan, Owner = settlement.Party };
        settlement.SettlementComponent = town;
        settlement.Town = town;
        return settlement;
    }

    private static Location CreateLocation(string id, LocationComplex locationComplex)
    {
        return new Location(
            id,
            new TextObject(id),
            new TextObject(id),
            100,
            true,
            false,
            "CanAlways",
            "CanAlways",
            "CanAlways",
            "CanAlways",
            new string[4],
            locationComplex);
    }

    private static LocationCharacter CreateLocationCharacter(Hero hero)
    {
        return LocationCharacterFactory.Create(
            hero.CharacterObject,
            originParty: null,
            specialItem: null,
            spawnTag: "npc_common",
            actionSetCode: null,
            behaviorsMethodName: null,
            characterRelation: (int)LocationCharacter.CharacterRelations.Neutral,
            fixedLocation: false,
            useCivilianEquipment: true);
    }
}
