using Missions.Naval;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalNpcFleetSpawnerTests
{
    [Theory]
    [InlineData(false, true, true, 2, false, NavalNpcFleetSpawner.WaitingForHost)]
    [InlineData(false, false, false, 0, true, NavalNpcFleetSpawner.WaitingForHost)]
    [InlineData(true, true, true, 2, true, NavalNpcFleetSpawner.AlreadyFielded)]
    [InlineData(true, false, true, 2, false, NavalNpcFleetSpawner.WaitingForDeployment)]
    [InlineData(true, true, false, 2, false, NavalNpcFleetSpawner.WaitingForReserve)]
    [InlineData(true, true, true, 0, false, NavalNpcFleetSpawner.WaitingForReserve)]
    [InlineData(true, true, true, 1, false, NavalNpcFleetSpawner.Ready)]
    public void Decide_FieldsTheAiFleetOnlyOnTheHostAfterDeploymentWithTheNpcReserve(
        bool isLocalHost, bool deploymentFinished, bool reservePopulated, int npcParties, bool npcHullsRegistered, string expected)
    {
        Assert.Equal(expected, NavalNpcFleetSpawner.Decide(isLocalHost, deploymentFinished, reservePopulated, npcParties, npcHullsRegistered));
    }

    [Theory]
    [InlineData(4, 3, 40, 8, 3)]
    [InlineData(2, 3, 40, 8, 2)]
    [InlineData(4, 3, 2, 8, 2)]
    [InlineData(4, 3, 40, 1, 1)]
    [InlineData(12, 12, 400, 11, 8)]
    [InlineData(4, 3, 0, 8, 0)]
    [InlineData(0, 3, 40, 8, 0)]
    public void ShipCount_FollowsVanillasLimitShipTroopAndFormationCaps(int limit, int ships, int troops, int formations, int expected)
    {
        Assert.Equal(expected, NavalNpcFleetSpawner.ShipCount(limit, ships, troops, formations));
    }
}
