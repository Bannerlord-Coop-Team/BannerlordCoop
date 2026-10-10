using Missions.Battles;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalMigrationRulesTests
{
    [Theory]
    [InlineData(Mission.State.Continuing, true)]
    [InlineData(Mission.State.EndingNextFrame, false)]
    [InlineData(Mission.State.Over, false)]
    public void AgentRoutReporter_BroadcastsOnlyRoutsFromALiveBattleNotTheLeaversTeardown(Mission.State state, bool expected)
    {
        Assert.Equal(expected, AgentRoutReporter.IsBattlefieldRout(state));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void BattleAuthorityMigrator_ChargesAdoptedLandFormationsButNotShipCrews(bool isShipCrew, bool expected)
    {
        Assert.Equal(expected, BattleAuthorityMigrator.ShouldChargeAdoptedFormation(isShipCrew));
    }

    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(false, false, false, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, true, false)]
    public void AgentStationUseReplicator_DropsAnAppliedSeatOnceThePuppetIsAdoptedDeadOrItsHullSinks(bool alive, bool ownedHere,
        bool hullSinking, bool expected)
    {
        Assert.Equal(expected, AgentStationUseReplicator.KeepsAppliedSeat(alive, ownedHere, hullSinking));
    }
}
