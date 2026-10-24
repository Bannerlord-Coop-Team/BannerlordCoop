#if COOP_NAVAL
using Common.Messaging;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Missions.Battles;
using Missions.Naval;
using Moq;
using NavalDLC;
using NavalDLC.Missions.MissionLogics;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>R41a, R41a-2 S: a naval mission that can't be set up must end instead of hanging in deployment or retrying.</summary>
public class CoopNavalSetupGuardTests
{
    // Mission.AfterStart resets the state after EarlyStart, so the guard ends the mission from its first pre-display tick.
    [Fact]
    public void Guard_EndsTheMissionFromItsPreDisplayTick()
    {
        Assert.NotNull(AccessTools.DeclaredMethod(typeof(CoopNavalSetupGuard), nameof(CoopNavalSetupGuard.OnPreDisplayMissionTick)));
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

    // R41a-2 S: a failed naval setup re-ran every tick and spawned 161 hulls; the first failure ends the mission instead, and
    // the player isn't told its reserve was missing.
    [Fact]
    public void FailSetup_MarksTheSetupFailedAndTellsThePlayerOnce()
    {
        var broker = new Mock<IMessageBroker>();
        var guard = CreateGuard(broker);

        bool first = guard.FailSetup();
        bool second = guard.FailSetup();
        bool endedAgain = guard.RequestEnd(NavalSetupEndReason.PlayerTeamNotAllocatable);

        Assert.True(first);
        Assert.False(second);
        Assert.False(endedAgain);
        Assert.True(guard.SetupFailed);
        broker.Verify(b => b.Publish(guard, It.IsAny<SendInformationMessage>()), Times.Once);
        broker.Verify(b => b.Publish(guard, It.Is<SendInformationMessage>(m => m.Text == CoopNavalSetupGuard.SetupFailedMessage)),
            Times.Once);
    }

    // R41a fix-loop C5: a shipless player team asks the guard to end the mission each tick vanilla retries the allocation, and
    // the player is told once; only a missing reserve says so.
    [Theory]
    [InlineData(NavalSetupEndReason.MissingReserve, true)]
    [InlineData(NavalSetupEndReason.PlayerTeamNotAllocatable, false)]
    public void RequestEnd_OnlyTheFirstRequestEndsTheMission_WithAMessageForItsReason(NavalSetupEndReason reason, bool reserveMessage)
    {
        var broker = new Mock<IMessageBroker>();
        var guard = CreateGuard(broker);
        string expected = reserveMessage ? CoopBattleMissionSpawnHandler.InvalidPlayerReserveMessage : CoopNavalSetupGuard.SetupFailedMessage;

        bool first = guard.RequestEnd(reason);
        bool second = guard.RequestEnd(reason);

        Assert.True(first);
        Assert.False(second);
        Assert.False(guard.SetupFailed);
        broker.Verify(b => b.Publish(guard, It.IsAny<SendInformationMessage>()), Times.Once);
        broker.Verify(b => b.Publish(guard, It.Is<SendInformationMessage>(m => m.Text == expected)), Times.Once);
    }

    private static CoopNavalSetupGuard CreateGuard(Mock<IMessageBroker> broker) =>
        new CoopNavalSetupGuard(new CoopTroopSupplier("battle", BattleSideEnum.Attacker, Mock.Of<IObjectManager>(),
            Mock.Of<IBattleAgentBudget>()), BattleSideEnum.Attacker, broker.Object);
}
#endif
