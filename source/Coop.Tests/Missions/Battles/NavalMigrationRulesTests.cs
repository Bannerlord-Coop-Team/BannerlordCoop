using Missions.Battles;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalMigrationRulesTests
{
    [Theory]
    [InlineData(false, Mission.State.Continuing, true)]
    [InlineData(true, Mission.State.Continuing, false)]
    [InlineData(true, Mission.State.EndingNextFrame, false)]
    [InlineData(false, Mission.State.EndingNextFrame, false)]
    [InlineData(false, Mission.State.Over, false)]
    public void AgentRoutReporter_BroadcastsOnlyRoutsFromALiveBattleNotTheLeaversTeardown(
        bool missionEnded, Mission.State state, bool expected)
    {
        Assert.Equal(expected, AgentRoutReporter.IsBattlefieldRout(missionEnded, state));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void BattleAuthorityMigrator_ChargesAdoptedLandFormationsButNotShipCrews(bool isShipCrew, bool expected)
    {
        Assert.Equal(expected, BattleAuthorityMigrator.ShouldChargeAdoptedFormation(isShipCrew));
    }

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    public void AgentStationUseReplicator_DropsAnAppliedSeatOnceThePuppetIsAdopted(bool alive, bool ownedHere, bool expected)
    {
        Assert.Equal(expected, AgentStationUseReplicator.KeepsAppliedSeat(alive, ownedHere));
    }
}
