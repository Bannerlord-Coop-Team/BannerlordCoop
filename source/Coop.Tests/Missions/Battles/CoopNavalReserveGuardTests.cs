#if DEBUG
using Common.Messaging;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Missions.Naval;
using Moq;
using NavalDLC;
using NavalDLC.Missions.MissionLogics;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>R41a: a returner with no reserve must leave its naval mission instead of hanging in deployment.</summary>
public class CoopNavalReserveGuardTests
{
    // Mission.AfterStart resets the state after EarlyStart, so the guard ends the mission from its first pre-display tick.
    [Fact]
    public void Guard_EndsTheMissionFromItsPreDisplayTick()
    {
        Assert.NotNull(AccessTools.DeclaredMethod(typeof(CoopNavalReserveGuard), nameof(CoopNavalReserveGuard.OnPreDisplayMissionTick)));
    }

    [Fact]
    public void Target_ThePlayerTeamsInitialAllocationExists()
    {
        Assert.NotNull(AccessTools.DeclaredMethod(typeof(NavalTeamSideSpawnContext),
            nameof(NavalTeamSideSpawnContext.AllocateAndDeployInitialTroopsOfPlayerTeam)));
    }

    [Fact]
    public void Target_ThePlayerOriginLookupExists()
    {
        Assert.NotNull(AccessTools.DeclaredMethod(typeof(NavalAgentsLogic), nameof(NavalAgentsLogic.FindTroopOrigin)));
    }

    [Fact]
    public void Target_TheDeploymentTeamSetupAndTickExist()
    {
        // SetupTeams is private and this project doesn't publicize TaleWorlds.MountAndBlade.
        Assert.NotNull(AccessTools.DeclaredMethod(typeof(DeploymentMissionController), "SetupTeams"));
        Assert.NotNull(AccessTools.DeclaredMethod(typeof(DeploymentMissionController), nameof(DeploymentMissionController.OnMissionTick)));
    }

    // Vanilla assigns no ship to a team with no troops (R41a), and a reserve without the player hero has no player origin
    // (R41a-2 S); either way its allocation throws every tick.
    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void AllocatesPlayerTeam_NeedsAShipAndThePlayerOrigin(bool hasShip, bool hasPlayerOrigin, bool expected)
    {
        Assert.Equal(expected, CoopNavalPlayerTeamAllocationPatch.AllocatesPlayerTeam(hasShip, hasPlayerOrigin));
    }

    // R41a-2 S: a failed naval setup re-ran every tick and spawned 161 hulls; the first failure ends the mission instead.
    [Fact]
    public void FailSetup_SkipsTheDeploymentTickAndTellsThePlayerOnce()
    {
        var broker = new Mock<IMessageBroker>();
        var supplier = new CoopTroopSupplier("battle", BattleSideEnum.Attacker, Mock.Of<IObjectManager>(), Mock.Of<IBattleAgentBudget>());
        var guard = new CoopNavalReserveGuard(supplier, BattleSideEnum.Attacker, broker.Object);
        bool ticksBefore = CoopNavalTeamSetupFailurePatches.RunsDeploymentTick(guard.SetupFailed);

        bool first = guard.FailSetup();
        bool second = guard.FailSetup();
        bool endedAgain = guard.RequestEnd();

        Assert.True(ticksBefore);
        Assert.True(first);
        Assert.False(second);
        Assert.False(endedAgain);
        Assert.False(CoopNavalTeamSetupFailurePatches.RunsDeploymentTick(guard.SetupFailed));
        broker.Verify(b => b.Publish(guard, It.IsAny<SendInformationMessage>()), Times.Once);
    }

    // R41a fix-loop C5: a shipless player team asks the guard to end the mission each tick vanilla retries the allocation, and
    // the player is told once.
    [Fact]
    public void RequestEnd_OnlyTheFirstRequestEndsTheMissionAndTellsThePlayer()
    {
        var broker = new Mock<IMessageBroker>();
        var supplier = new CoopTroopSupplier("battle", BattleSideEnum.Attacker, Mock.Of<IObjectManager>(), Mock.Of<IBattleAgentBudget>());
        var guard = new CoopNavalReserveGuard(supplier, BattleSideEnum.Attacker, broker.Object);

        bool first = guard.RequestEnd();
        bool second = guard.RequestEnd();

        Assert.True(first);
        Assert.False(second);
        broker.Verify(b => b.Publish(guard, It.IsAny<SendInformationMessage>()), Times.Once);
    }
}
#endif
