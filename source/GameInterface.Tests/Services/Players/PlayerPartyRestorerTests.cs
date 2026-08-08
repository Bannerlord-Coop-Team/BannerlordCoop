using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Tests.Bootstrap;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.Players;

public class PlayerPartyRestorerTests
{
    public PlayerPartyRestorerTests()
    {
        GameBootStrap.Initialize();
    }

    [Fact]
    public void Restore_MissingPlayerState_AddsMembershipsAndLeader()
    {
        var (hero, party, clan, character) = CreatePlayerGraph();
        var restorer = new PlayerPartyRestorer(Mock.Of<IObjectManager>());

        restorer.Restore(hero, party);

        Assert.Contains(hero, clan.Heroes);
        Assert.Contains(hero, clan.AliveLords);
        Assert.Equal(1, party.MemberRoster.GetTroopCount(character));
        Assert.Same(hero, party.LeaderHero);
        Assert.Same(hero, party.LordPartyComponent.Owner);
        Assert.Same(party, hero.PartyBelongedTo);
    }

    [Fact]
    public void Restore_ExistingPlayerState_DoesNotAddDuplicates()
    {
        var (hero, party, clan, character) = CreatePlayerGraph();
        var restorer = new PlayerPartyRestorer(Mock.Of<IObjectManager>());

        restorer.Restore(hero, party);
        restorer.Restore(hero, party);

        Assert.Equal(1, clan.Heroes.Count(x => x == hero));
        Assert.Equal(1, clan.AliveLords.Count(x => x == hero));
        Assert.Equal(1, party.MemberRoster.GetTroopCount(character));
        Assert.Same(hero, party.LeaderHero);
    }

    [Fact]
    public void Restore_StaleHeroPartyRoot_PointsHeroAtRestoredParty()
    {
        var (hero, party, _, _) = CreatePlayerGraph();
        var (_, staleParty, _, _) = CreatePlayerGraph();
        var restorer = new PlayerPartyRestorer(Mock.Of<IObjectManager>());
        restorer.Restore(hero, party);
        hero._partyBelongedTo = staleParty;

        restorer.Restore(hero, party);

        Assert.Same(party, hero.PartyBelongedTo);
    }

    [Fact]
    public void TryRestore_StalePartyId_ReassociatesOwnedPartyWithoutReplacingHero()
    {
        var (hero, party, _, _) = CreatePlayerGraph();
        var player = new Player("Controller", "Hero_Saved", "MobileParty_Stale", "Clan_Stale", "Character_Stale");
        var objectManager = new Mock<IObjectManager>();
        Hero resolvedHero = hero;
        MobileParty missingParty = null;
        string partyId = "MobileParty_Recovered";
        string clanId = "Clan_Recovered";
        string characterId = "Character_Recovered";
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging(player.HeroId, out resolvedHero))
            .Returns(true);
        objectManager
            .Setup(manager => manager.TryGetObject(player.MobilePartyId, out missingParty))
            .Returns(false);
        objectManager.Setup(manager => manager.TryGetId(party, out partyId)).Returns(true);
        objectManager.Setup(manager => manager.TryGetIdWithLogging(party, out partyId)).Returns(true);
        objectManager.Setup(manager => manager.TryGetIdWithLogging(hero.Clan, out clanId)).Returns(true);
        objectManager.Setup(manager => manager.TryGetIdWithLogging(hero.CharacterObject, out characterId)).Returns(true);

        var restorer = new PlayerPartyRestorer(
            objectManager.Object,
            () => new[] { party },
            _ => throw new InvalidOperationException("A live owned party should be reused"));

        Assert.True(restorer.TryRestore(player, out var restored));

        Assert.Equal(player.ControllerId, restored.ControllerId);
        Assert.Equal(player.HeroId, restored.HeroId);
        Assert.Equal(partyId, restored.MobilePartyId);
        Assert.Equal(clanId, restored.ClanId);
        Assert.Equal(characterId, restored.CharacterObjectId);
        Assert.Equal(1, party.MemberRoster.GetTroopCount(hero.CharacterObject));
        Assert.Same(hero, party.LeaderHero);
    }

    [Fact]
    public void TryRestore_MissingCaptiveParty_CreatesParkedLeaderlessParty()
    {
        var (hero, party, _, _) = CreatePlayerGraph();
        var captor = (PartyBase)FormatterServices.GetUninitializedObject(typeof(PartyBase));
        var captorParty = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
        var captivePosition = new CampaignVec2(new Vec2(12f, 34f), isOnLand: true);
        captor.MobileParty = captorParty;
        captorParty.Party = captor;
        captorParty.Position = captivePosition;
        party._position = captivePosition;
        hero._heroState = Hero.CharacterStates.Prisoner;
        hero.PartyBelongedToAsPrisoner = captor;
        party.IsActive = true;

        var player = new Player("Controller", "Hero_Saved", "MobileParty_Missing", "Clan_Saved", "Character_Saved");
        var objectManager = new Mock<IObjectManager>();
        Hero resolvedHero = hero;
        MobileParty missingParty = null;
        string partyId = "MobileParty_Recovered";
        string clanId = "Clan_Saved";
        string characterId = "Character_Saved";
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging(player.HeroId, out resolvedHero))
            .Returns(true);
        objectManager
            .Setup(manager => manager.TryGetObject(player.MobilePartyId, out missingParty))
            .Returns(false);
        objectManager.Setup(manager => manager.TryGetId(party, out partyId)).Returns(true);
        objectManager.Setup(manager => manager.TryGetIdWithLogging(party, out partyId)).Returns(true);
        objectManager.Setup(manager => manager.TryGetIdWithLogging(hero.Clan, out clanId)).Returns(true);
        objectManager.Setup(manager => manager.TryGetIdWithLogging(hero.CharacterObject, out characterId)).Returns(true);

        var restorer = new PlayerPartyRestorer(
            objectManager.Object,
            () => Array.Empty<MobileParty>(),
            _ => party);

        Assert.True(restorer.TryRestore(player, out var restored));

        Assert.Equal(player.HeroId, restored.HeroId);
        Assert.Equal(partyId, restored.MobilePartyId);
        Assert.Same(captor, hero.PartyBelongedToAsPrisoner);
        Assert.Equal(0, party.MemberRoster.GetTroopCount(hero.CharacterObject));
        Assert.Null(party.LeaderHero);
        Assert.Null(hero.PartyBelongedTo);
        Assert.Equal(captivePosition, party.Position);
        Assert.False(party.IsActive);
    }

    [Fact]
    public void TryRestore_NullPartyComponentBacklink_RepairsBacklink()
    {
        var (hero, party, _, _) = CreatePlayerGraph();
        party.PartyComponent.MobileParty = null;
        var player = new Player("Controller", "Hero_Saved", "MobileParty_Saved", "Clan_Saved", "Character_Saved");
        var objectManager = CreateObjectManager(player, hero, party);

        var restorer = new PlayerPartyRestorer(
            objectManager.Object,
            () => Array.Empty<MobileParty>(),
            _ => throw new InvalidOperationException("The saved party should be reused"));

        Assert.True(restorer.TryRestore(player, out _));
        Assert.Same(party, party.PartyComponent.MobileParty);
    }

    [Fact]
    public void TryRestore_PartyComponentBacklinkToDifferentParty_RejectsParty()
    {
        var (hero, party, _, _) = CreatePlayerGraph();
        party.PartyComponent.MobileParty =
            (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
        var player = new Player("Controller", "Hero_Saved", "MobileParty_Saved", "Clan_Saved", "Character_Saved");
        var objectManager = CreateObjectManager(player, hero, party);

        var restorer = new PlayerPartyRestorer(
            objectManager.Object,
            () => new[] { party },
            _ => null);

        Assert.False(restorer.TryRestore(player, out _));
    }

    [Fact]
    public void TryRestore_PartyOwnedByAnotherHero_DoesNotReassociateIt()
    {
        var (hero, party, _, _) = CreatePlayerGraph();
        var otherHero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
        var otherOwnerComponent = new LordPartyComponent(otherHero, null, null);
        otherOwnerComponent.MobileParty = party;
        party._partyComponent = otherOwnerComponent;

        var player = new Player("Controller", "Hero_Saved", "MobileParty_Stale", "Clan_Saved", "Character_Saved");
        var objectManager = new Mock<IObjectManager>();
        Hero resolvedHero = hero;
        MobileParty missingParty = null;
        string clanId = "Clan_Saved";
        string characterId = "Character_Saved";
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging(player.HeroId, out resolvedHero))
            .Returns(true);
        objectManager
            .Setup(manager => manager.TryGetObject(player.MobilePartyId, out missingParty))
            .Returns(false);
        objectManager.Setup(manager => manager.TryGetIdWithLogging(hero.Clan, out clanId)).Returns(true);
        objectManager.Setup(manager => manager.TryGetIdWithLogging(hero.CharacterObject, out characterId)).Returns(true);

        var createCalled = false;
        var restorer = new PlayerPartyRestorer(
            objectManager.Object,
            () => new[] { party },
            _ =>
            {
                createCalled = true;
                return null;
            });

        Assert.False(restorer.TryRestore(player, out _));
        Assert.True(createCalled);
    }

    private static (Hero Hero, MobileParty Party, Clan Clan, CharacterObject Character) CreatePlayerGraph()
    {
        var clan = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));
        clan._heroesCache = new MBList<Hero>();
        clan._aliveLordsCache = new MBList<Hero>();
        clan._deadLordsCache = new MBList<Hero>();

        var hero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));
        hero._clan = clan;
        hero._heroState = Hero.CharacterStates.Active;

        var character = (CharacterObject)FormatterServices.GetUninitializedObject(typeof(CharacterObject));
        character.HeroObject = hero;
        hero._characterObject = character;

        var party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
        var partyBase = (PartyBase)FormatterServices.GetUninitializedObject(typeof(PartyBase));
        party.Party = partyBase;
        partyBase.MobileParty = party;
        partyBase.MemberRoster = new TroopRoster();
        partyBase.PrisonRoster = new TroopRoster();
        partyBase.ItemRoster = new ItemRoster();
        hero._partyBelongedTo = party;

        var component = new LordPartyComponent(hero, null, null);
        component.MobileParty = party;
        party._partyComponent = component;

        return (hero, party, clan, character);
    }

    private static Mock<IObjectManager> CreateObjectManager(Player player, Hero hero, MobileParty party)
    {
        var objectManager = new Mock<IObjectManager>();
        Hero resolvedHero = hero;
        MobileParty resolvedParty = party;
        string partyId = player.MobilePartyId;
        string clanId = player.ClanId;
        string characterId = player.CharacterObjectId;
        objectManager
            .Setup(manager => manager.TryGetObjectWithLogging(player.HeroId, out resolvedHero))
            .Returns(true);
        objectManager
            .Setup(manager => manager.TryGetObject(player.MobilePartyId, out resolvedParty))
            .Returns(true);
        objectManager.Setup(manager => manager.TryGetIdWithLogging(party, out partyId)).Returns(true);
        objectManager.Setup(manager => manager.TryGetIdWithLogging(hero.Clan, out clanId)).Returns(true);
        objectManager.Setup(manager => manager.TryGetIdWithLogging(hero.CharacterObject, out characterId)).Returns(true);
        return objectManager;
    }
}
