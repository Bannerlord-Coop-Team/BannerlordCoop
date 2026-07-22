using GameInterface.Services.Heroes.Interfaces;
using GameInterface.Tests.Bootstrap;
using System.Linq;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Library;
using Xunit;

namespace GameInterface.Tests.Services.Heroes;

public class HeroInterfaceTests
{
    public HeroInterfaceTests()
    {
        GameBootStrap.Initialize();
    }

    [Fact]
    public void RestorePlayerMemberships_MissingMemberships_AddsHeroToClanAndParty()
    {
        var (hero, party, clan, character) = CreatePlayerGraph();

        HeroInterface.RestorePlayerMemberships(hero, party);

        Assert.Contains(hero, clan.Heroes);
        Assert.Contains(hero, clan.AliveLords);
        Assert.Equal(1, party.MemberRoster.GetTroopCount(character));
    }

    [Fact]
    public void RestorePlayerMemberships_ExistingMemberships_DoesNotAddDuplicates()
    {
        var (hero, party, clan, character) = CreatePlayerGraph();

        HeroInterface.RestorePlayerMemberships(hero, party);
        HeroInterface.RestorePlayerMemberships(hero, party);

        Assert.Equal(1, clan.Heroes.Count(x => x == hero));
        Assert.Equal(1, clan.AliveLords.Count(x => x == hero));
        Assert.Equal(1, party.MemberRoster.GetTroopCount(character));
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

        return (hero, party, clan, character);
    }
}
