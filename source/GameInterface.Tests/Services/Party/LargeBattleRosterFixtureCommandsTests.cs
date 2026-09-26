#if DEBUG
using Common.Util;
using GameInterface.Services.Party.Commands;
using GameInterface.Tests.Bootstrap;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using Xunit;

namespace GameInterface.Tests.Services.Party;

public class LargeBattleRosterFixtureCommandsTests
{
    [Theory]
    [InlineData(true, false, false, false, 0, 7, true, 7)]
    [InlineData(false, false, false, false, 0, 7, false, 0)]
    [InlineData(true, false, true, false, 0, 7, false, 0)]
    [InlineData(true, true, true, false, 1, 7, false, 0)]
    [InlineData(false, true, true, true, 1, 4, true, 3)]
    [InlineData(true, false, false, false, 0, 4, true, 4)]
    [InlineData(false, true, true, true, 5, 4, false, 0)]
    public void ExactRoster_RequiresHealthyLeaderExceptForHeroLessBandit(
        bool isBandit,
        bool hasLeader,
        bool hasHero,
        bool hasHealthyLeader,
        int healthyHeroes,
        int target,
        bool succeeds,
        int expectedTroops)
    {
        bool result = LargeBattleRosterFixtureCommands.TryGetFixtureTroopCount(
            "party", target, healthyHeroes, hasHealthyLeader, isBandit, hasLeader, hasHero,
            out int troops, out string error);

        Assert.Equal(succeeds, result);
        Assert.Equal(expectedTroops, troops);
        if (succeeds)
            Assert.Null(error);
        else
            Assert.NotNull(error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void ExactRoster_PreservesLeaderAndCompanionRoles(int fixtureTroops)
    {
        GameBootStrap.Initialize();
        var leader = CreateHero(100);
        var companion = CreateHero(1);
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        var partyBase = ObjectHelper.SkipConstructor<PartyBase>();
        party.Party = partyBase;
        partyBase.MobileParty = party;
        var component = ObjectHelper.SkipConstructor<LordPartyComponent>();
        component.MobileParty = party;
        component._leader = leader;
        component.Owner = leader;
        party._partyComponent = component;
        var roster = new TroopRoster(partyBase);
        partyBase.MemberRoster = roster;
        roster.AddToCounts(leader.CharacterObject, 1);
        roster.AddToCounts(companion.CharacterObject, 1);
        party.Scout = companion;
        var oldTroop = new CharacterObject();
        var fixtureTroop = new CharacterObject();
        roster.AddToCounts(oldTroop, 9, false, 2, 17);
        roster.AddToCounts(fixtureTroop, 8, false, 1, 40);

        LargeBattleRosterFixtureCommands.SetExactRoster(roster, fixtureTroop, fixtureTroops);

        Assert.Same(leader, party.LeaderHero);
        Assert.Same(companion, party.Scout);
        Assert.Same(party, leader.PartyBelongedTo);
        Assert.Same(party, companion.PartyBelongedTo);
        Assert.Equal(100, leader.HitPoints);
        Assert.Equal(1, companion.HitPoints);
        Assert.Equal(1, roster.GetElementNumber(leader.CharacterObject));
        Assert.Equal(1, roster.GetElementNumber(companion.CharacterObject));
        Assert.Equal(1, roster.GetElementWoundedNumber(roster.FindIndexOfTroop(companion.CharacterObject)));
        Assert.False(roster.Contains(oldTroop));
        Assert.Equal(fixtureTroops + 2, roster.TotalManCount);
        Assert.Equal(fixtureTroops + 1, roster.TotalHealthyCount);
        if (fixtureTroops == 0)
            Assert.False(roster.Contains(fixtureTroop));
        else
        {
            var element = roster.GetElementCopyAtIndex(roster.FindIndexOfTroop(fixtureTroop));
            Assert.Equal(fixtureTroops, element.Number);
            Assert.Equal(0, element.WoundedNumber);
            Assert.Equal(0, element.Xp);
        }
    }

    [Fact]
    public void ExactRoster_ReplacesHeroLessBanditRoster()
    {
        var roster = new TroopRoster();
        var oldTroop = new CharacterObject();
        var fixtureTroop = new CharacterObject();
        roster.AddToCounts(oldTroop, 15, false, 3);

        LargeBattleRosterFixtureCommands.SetExactRoster(roster, fixtureTroop, 7);

        Assert.Equal(1, roster.Count);
        Assert.False(roster.Contains(oldTroop));
        Assert.Equal(7, roster.GetElementNumber(fixtureTroop));
        Assert.Equal(7, roster.TotalHealthyCount);
    }

    private static Hero CreateHero(int hitPoints)
    {
        var hero = new Hero();
        var character = new CharacterObject();
        character.HeroObject = hero;
        hero._characterObject = character;
        hero._health = hitPoints;
        hero._heroState = Hero.CharacterStates.Active;
        return hero;
    }

}
#endif
