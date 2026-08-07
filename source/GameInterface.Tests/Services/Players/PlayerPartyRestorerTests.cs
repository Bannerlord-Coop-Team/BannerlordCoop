using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Tests.Bootstrap;
using Moq;
using System.Linq;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
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
        hero._partyBelongedTo = party;

        var component = new LordPartyComponent(hero, null, null);
        component.MobileParty = party;
        party._partyComponent = component;

        return (hero, party, clan, character);
    }
}
