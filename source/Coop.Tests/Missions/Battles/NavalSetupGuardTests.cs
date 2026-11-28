#if COOP_NAVAL
using Common.Messaging;
using GameInterface.Services.GameDebug.Messages;
using HarmonyLib;
using Missions.Naval;
using Moq;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>R41a, R41a-2 S, #4953: a naval mission that can't be set up must end instead of hanging in deployment or retrying.</summary>
public class NavalSetupGuardTests
{
    [Fact]
    public void Target_TheDeploymentTeamSetupExists()
    {
        // SetupTeams is private and this project doesn't publicize TaleWorlds.MountAndBlade.
        Assert.NotNull(AccessTools.DeclaredMethod(typeof(DeploymentMissionController), "SetupTeams"));
    }

    // R41a-2 S: a failed naval setup re-ran every tick and spawned 161 hulls; the first failure ends the mission instead, and
    // the player isn't told its reserve was missing.
    [Fact]
    public void End_SetupFailure_MarksTheSetupFailedAndTellsThePlayerOnce()
    {
        var broker = new Mock<IMessageBroker>();
        var guard = new NavalSetupGuard(broker.Object);

        bool first = guard.End(NavalSetupEndReason.TeamSetupFailed);
        bool second = guard.End(NavalSetupEndReason.TeamSetupFailed);
        bool endedAgain = guard.End(NavalSetupEndReason.PlayerTeamNotAllocatable);

        Assert.True(first);
        Assert.False(second);
        Assert.False(endedAgain);
        Assert.True(guard.SetupFailed);
        broker.Verify(b => b.Publish(guard, It.IsAny<SendInformationMessage>()), Times.Once);
        broker.Verify(b => b.Publish(guard, It.Is<SendInformationMessage>(m => m.Text == NavalSetupGuard.SetupFailedMessage)),
            Times.Once);
    }

    // The allocation prefix asks for the end from inside SetupTeams, which can then throw; the failure must still latch, or
    // the deployment controller runs the side deployment again on its next tick.
    [Fact]
    public void End_SetupFailureAfterAnEarlierEnd_StillMarksTheSetupFailed()
    {
        var broker = new Mock<IMessageBroker>();
        var guard = new NavalSetupGuard(broker.Object);

        bool ended = guard.End(NavalSetupEndReason.PlayerTeamNotAllocatable);
        bool failed = guard.End(NavalSetupEndReason.TeamSetupFailed);

        Assert.True(ended);
        Assert.True(failed);
        Assert.True(guard.SetupFailed);
        broker.Verify(b => b.Publish(guard, It.IsAny<SendInformationMessage>()), Times.Once);
    }

    // A shipless player team asks the guard to end the mission and the player is told once; the reserve message belongs to
    // the coop battle guard, so a raid's guard reports every reason as a failed setup.
    [Theory]
    [InlineData(NavalSetupEndReason.MissingReserve)]
    [InlineData(NavalSetupEndReason.PlayerTeamNotAllocatable)]
    public void End_OnlyTheFirstRequestEndsTheMission_WithTheSetupFailedMessage(NavalSetupEndReason reason)
    {
        var broker = new Mock<IMessageBroker>();
        var guard = new NavalSetupGuard(broker.Object);

        bool first = guard.End(reason);
        bool second = guard.End(reason);

        Assert.True(first);
        Assert.False(second);
        Assert.False(guard.SetupFailed);
        broker.Verify(b => b.Publish(guard, It.IsAny<SendInformationMessage>()), Times.Once);
        broker.Verify(b => b.Publish(guard, It.Is<SendInformationMessage>(m => m.Text == NavalSetupGuard.SetupFailedMessage)),
            Times.Once);
    }
}
#endif
