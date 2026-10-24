#if DEBUG
using Common.Network;
using GameInterface.Services.MapEvents.NavalScenarios;
using GameInterface.Services.Missions;
using Moq;
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

    // R48: reserve=n leaves at least n regulars beyond every crew place, so the victim has troops to reinforce with.
    [Theory]
    [InlineData(33, 104, 10, 8)]
    [InlineData(33, 104, 1, 7)]
    [InlineData(114, 104, 10, 0)]
    [InlineData(120, 104, 10, 0)]
    [InlineData(0, 0, 5, 1)]
    public void ReserveTemplatesMissing_AddsWholeTemplatesUntilTheReserveIsPastTheCrewCapacity(int regulars, int crewCapacity,
        int reserve, int expected)
    {
        int templates = NavalScenarioStager.ReserveTemplatesMissing(regulars, crewCapacity, reserve);

        Assert.Equal(expected, templates);
        Assert.True(regulars + (templates * NavalScenarioStager.TroopsPerTemplate) >= crewCapacity + reserve);
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

    // R43: boarding was staged while both clients were still in the finished ramming mission, and neither entered it.
    [Fact]
    public void TryStage_WhileAListedPlayerIsStillInAMission_RefusesBeforeTouchingTheCampaign()
    {
        var network = new Mock<INetwork>(MockBehavior.Strict);
        var stager = Stager(network.Object, "testclient2");
        var config = new NavalScenarioConfig { Preset = "boarding", ControllerIds = new[] { "testclient1", "testclient2" } };

        Assert.False(stager.TryStage(config, out var output));

        Assert.Contains("testclient2", output);
        Assert.DoesNotContain("testclient1", output);
        Assert.Contains("coop.debug.map_event.leave_battle", output);
    }

    // Another player's mission doesn't block staging for the listed ones.
    [Fact]
    public void PlayersStillInBattle_IsNullWhenNoListedPlayerIsInAMission()
    {
        Assert.Null(Stager(null, "someone_else").PlayersStillInBattle(new[] { "testclient1", "testclient2" }));
    }

    // A client container builds the stager without mission membership, which only the server registers.
    [Fact]
    public void PlayersStillInBattle_WithoutMissionMembership_RefusesNothing()
    {
        var stager = new NavalScenarioStager(null, null, null, null, null, null, null);

        Assert.Null(stager.PlayersStillInBattle(new[] { "testclient1" }));
    }

    // A stager whose mission membership holds only the given controllers; the campaign services stay null.
    private static NavalScenarioStager Stager(INetwork? network, params string[] inMission)
    {
        var membership = new Mock<IMissionMembershipRegistry>();
        membership.Setup(registry => registry.IsControllerInMission(It.IsAny<string>()))
            .Returns<string>(controllerId => Array.IndexOf(inMission, controllerId) >= 0);
        return new NavalScenarioStager(null, network, null, null, null, null, null, membership.Object);
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
