using Missions.Battles;
using TaleWorlds.Core;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class DefenderMissionAcknowledgementContractTests
{
    [Fact]
    public void IsReady_AcceptsACommittedDefenderSiegeAfterTheNativeDeploymentControllerIsRemoved()
    {
        Assert.True(DefenderMissionAcknowledgementContract.IsReady(
            isClient: true,
            missionActive: true,
            activeCoopSiegeAssault: true,
            battleSide: BattleSideEnum.Defender,
            localMainAgent: true,
            deploymentCommitted: true,
            controllerReady: true));
    }

    [Theory]
    [InlineData(false, true, true, BattleSideEnum.Defender, true, true, true)]
    [InlineData(true, false, true, BattleSideEnum.Defender, true, true, true)]
    [InlineData(true, true, false, BattleSideEnum.Defender, true, true, true)]
    [InlineData(true, true, true, BattleSideEnum.Attacker, true, true, true)]
    [InlineData(true, true, true, BattleSideEnum.Defender, false, true, true)]
    [InlineData(true, true, true, BattleSideEnum.Defender, true, false, true)]
    [InlineData(true, true, true, BattleSideEnum.Defender, true, true, false)]
    public void IsReady_RejectsEveryMissingMissionReadinessBoundary(
        bool isClient,
        bool missionActive,
        bool activeCoopSiegeAssault,
        BattleSideEnum battleSide,
        bool localMainAgent,
        bool deploymentCommitted,
        bool controllerReady)
    {
        Assert.False(DefenderMissionAcknowledgementContract.IsReady(
            isClient,
            missionActive,
            activeCoopSiegeAssault,
            battleSide,
            localMainAgent,
            deploymentCommitted,
            controllerReady));
    }
}
