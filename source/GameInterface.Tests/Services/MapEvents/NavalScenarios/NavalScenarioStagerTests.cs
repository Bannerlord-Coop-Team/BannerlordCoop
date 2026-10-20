#if DEBUG
using GameInterface.Services.MapEvents.NavalScenarios;
using System;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents.NavalScenarios;

public class NavalScenarioStagerTests
{
    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, 3, 2)]
    [InlineData(3, 3, 0)]
    [InlineData(5, 3, 0)]
    public void ShipsMissing_TopsUpWithoutRemovingAPlayersShips(int ships, int wanted, int expected)
    {
        Assert.Equal(expected, NavalScenarioStager.ShipsMissing(ships, wanted));
    }

    [Theory]
    [InlineData(0, 3, 3)]
    [InlineData(11, 3, 2)]
    [InlineData(12, 3, 2)]
    [InlineData(22, 3, 1)]
    [InlineData(23, 3, 1)]
    [InlineData(33, 3, 0)]
    [InlineData(80, 3, 0)]
    [InlineData(0, 6, 6)]
    public void TemplatesMissing_AddsWholeTemplatesUntilThePresetTroopCountIsReached(int regulars, int templates, int expected)
    {
        Assert.Equal(expected, NavalScenarioStager.TemplatesMissing(regulars, templates));
    }

    // Once the fleet is held in the player's conversation or a map event exists, neither engage nor the fallback may run.
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, true)]
    public void IsEncounterUnderWay_StopsTheEngagePushOnceAConversationOrMapEventExists(bool enemyInMapEvent, bool playerInMapEvent,
        bool enemyInConversation, bool expected)
    {
        Assert.Equal(expected, NavalScenarioStager.IsEncounterUnderWay(enemyInMapEvent, playerInMapEvent, enemyInConversation));
    }

    // BlockadeBattleMapEvent.CheckLiftingBlockade lifts the blockade with no battle above 1.2 times the blockade's sea power.
    [Theory]
    [InlineData(100f, 100f, false)]
    [InlineData(120f, 100f, false)]
    [InlineData(121f, 100f, true)]
    [InlineData(1f, 0f, true)]
    public void IsBlockadeLiftedWithoutBattle_MatchesVanillasLiftRatio(float attackers, float blockade, bool lifted)
    {
        Assert.Equal(lifted, NavalScenarioStager.IsBlockadeLiftedWithoutBattle(attackers, blockade));
    }

    [Theory]
    [InlineData(true, true, NavalScenarioStager.BlockadeLordStanding.Excluded)]
    [InlineData(true, false, NavalScenarioStager.BlockadeLordStanding.Excluded)]
    [InlineData(false, true, NavalScenarioStager.BlockadeLordStanding.AtWarWithBoth)]
    [InlineData(false, false, NavalScenarioStager.BlockadeLordStanding.ThirdFaction)]
    public void ClassifyBlockadeLord_NeverUsesTheTownsOrPlayersFaction(bool ownFaction, bool atWar,
        NavalScenarioStager.BlockadeLordStanding expected)
    {
        Assert.Equal(expected, NavalScenarioStager.ClassifyBlockadeLord(ownFaction, atWar));
    }

    [Fact]
    public void PickBlockadeLord_PrefersALordAtWarWithBothOverANearerThirdFaction()
    {
        var lords = new[] { Lord("third", ThirdFaction, 1f), Lord("far_at_war", AtWar, 50f), Lord("near_at_war", AtWar, 9f) };

        Assert.Equal("near_at_war", Pick(lords, allowThirdFaction: true)?.Id);
        Assert.Equal("near_at_war", Pick(lords, allowThirdFaction: false)?.Id);
    }

    [Fact]
    public void PickBlockadeLord_TakesTheNearestThirdFactionOnlyWhenAllowed()
    {
        var lords = new[] { Lord("own", Excluded, 0f), Lord("far_third", ThirdFaction, 30f), Lord("near_third", ThirdFaction, 4f) };

        Assert.Equal("near_third", Pick(lords, allowThirdFaction: true)?.Id);
        Assert.Null(Pick(lords, allowThirdFaction: false));
    }

    [Fact]
    public void PickBlockadeLord_FindsNoneAmongExcludedLords()
    {
        Assert.Null(Pick(new[] { Lord("town", Excluded, 1f), Lord("player", Excluded, 2f) }, allowThirdFaction: true));
        Assert.Null(Pick(Array.Empty<TestLord>(), allowThirdFaction: true));
    }

    private const NavalScenarioStager.BlockadeLordStanding Excluded = NavalScenarioStager.BlockadeLordStanding.Excluded;
    private const NavalScenarioStager.BlockadeLordStanding AtWar = NavalScenarioStager.BlockadeLordStanding.AtWarWithBoth;
    private const NavalScenarioStager.BlockadeLordStanding ThirdFaction = NavalScenarioStager.BlockadeLordStanding.ThirdFaction;

    private static TestLord Lord(string id, NavalScenarioStager.BlockadeLordStanding standing, float distanceSquared) =>
        new TestLord(id, standing, distanceSquared);

    private static TestLord? Pick(TestLord[] lords, bool allowThirdFaction) =>
        NavalScenarioStager.PickBlockadeLord(lords, lord => lord.Standing, lord => lord.DistanceSquared, allowThirdFaction);

    // A lord party reduced to what the pick reads.
    private sealed class TestLord
    {
        public TestLord(string id, NavalScenarioStager.BlockadeLordStanding standing, float distanceSquared)
        {
            Id = id;
            Standing = standing;
            DistanceSquared = distanceSquared;
        }

        public string Id { get; }
        public NavalScenarioStager.BlockadeLordStanding Standing { get; }
        public float DistanceSquared { get; }
    }
}
#endif
