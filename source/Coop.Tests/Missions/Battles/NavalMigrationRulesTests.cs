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
    [InlineData(true, true, false, false, false, true)]
    [InlineData(false, true, false, false, false, false)]
    [InlineData(true, false, false, false, false, false)]
    [InlineData(true, true, true, false, false, false)]
    [InlineData(true, true, false, true, false, false)]
    [InlineData(true, true, false, false, true, false)]
    public void AgentStationUseReplicator_DropsAnAppliedSeatOnceThePuppetIsAdoptedDeadWithdrawnOrItsHullSinksOrLeaves(bool alive,
        bool registered, bool ownedHere, bool hullSinking, bool hullRemoved, bool expected)
    {
        Assert.Equal(expected, AgentStationUseReplicator.KeepsAppliedSeat(alive, registered, ownedHere, hullSinking, hullRemoved));
    }
}
