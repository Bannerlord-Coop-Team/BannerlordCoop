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
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>R41a, R41a-2 S: a naval mission that can't be set up must end instead of hanging in deployment or retrying.</summary>
public class CoopNavalSetupGuardTests
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
        var guard = CreateGuard(broker);

        bool first = guard.End(NavalSetupEndReason.TeamSetupFailed);
        bool second = guard.End(NavalSetupEndReason.TeamSetupFailed);
        bool endedAgain = guard.End(NavalSetupEndReason.PlayerTeamNotAllocatable);

        Assert.True(first);
        Assert.False(second);
        Assert.False(endedAgain);
        Assert.True(guard.SetupFailed);
        broker.Verify(b => b.Publish(guard, It.IsAny<SendInformationMessage>()), Times.Once);
        broker.Verify(b => b.Publish(guard, It.Is<SendInformationMessage>(m => m.Text == CoopNavalSetupGuard.SetupFailedMessage)),
            Times.Once);
    }

    // The allocation prefix asks for the end from inside SetupTeams, which can then throw; the failure must still latch, or
    // the deployment controller runs the side deployment again on its next tick.
    [Fact]
    public void End_SetupFailureAfterAnEarlierEnd_StillMarksTheSetupFailed()
    {
        var broker = new Mock<IMessageBroker>();
        var guard = CreateGuard(broker);

        bool ended = guard.End(NavalSetupEndReason.PlayerTeamNotAllocatable);
        bool failed = guard.End(NavalSetupEndReason.TeamSetupFailed);

        Assert.True(ended);
        Assert.True(failed);
        Assert.True(guard.SetupFailed);
        broker.Verify(b => b.Publish(guard, It.IsAny<SendInformationMessage>()), Times.Once);
    }

    // A shipless player team asks the guard to end the mission and the player is told once; only a missing reserve says so.
    [Theory]
    [InlineData(NavalSetupEndReason.MissingReserve, true)]
    [InlineData(NavalSetupEndReason.PlayerTeamNotAllocatable, false)]
    public void End_OnlyTheFirstRequestEndsTheMission_WithAMessageForItsReason(NavalSetupEndReason reason, bool reserveMessage)
    {
        var broker = new Mock<IMessageBroker>();
        var guard = CreateGuard(broker);
        string expected = reserveMessage ? CoopBattleMissionSpawnHandler.InvalidPlayerReserveMessage : CoopNavalSetupGuard.SetupFailedMessage;

        bool first = guard.End(reason);
        bool second = guard.End(reason);

        Assert.True(first);
        Assert.False(second);
        Assert.False(guard.SetupFailed);
        broker.Verify(b => b.Publish(guard, It.IsAny<SendInformationMessage>()), Times.Once);
        broker.Verify(b => b.Publish(guard, It.Is<SendInformationMessage>(m => m.Text == expected)), Times.Once);
    }

    // An unpopulated supplier reports troops remaining forever, so the guard empties it before the naval initial pull spins.
    [Fact]
    public void EarlyStart_UnpopulatedReserve_PopulatesItEmptyAndEndsTheMission()
    {
        var broker = new Mock<IMessageBroker>();
        var supplier = CreateSupplier();
        var guard = new CoopNavalSetupGuard(supplier, BattleSideEnum.Attacker, broker.Object);

        guard.EarlyStart();

        Assert.True(supplier.IsPopulated);
        Assert.False(guard.ReservePresent);
        broker.Verify(b => b.Publish(guard, It.Is<SendInformationMessage>(m =>
            m.Text == CoopBattleMissionSpawnHandler.InvalidPlayerReserveMessage)), Times.Once);
    }

    [Fact]
    public void EarlyStart_OwnReserveWithTroops_KeepsTheMission()
    {
        var broker = new Mock<IMessageBroker>();
        var supplier = CreateSupplier();
        supplier.SetReserve(new[]
        {
            new PartyReserve("own_party", 0, new[] { new TroopReserveEntry(1, "imperial_recruit", 0) }, isReceiverPlayerParty: true),
        }, 1, 1, 1);
        var guard = new CoopNavalSetupGuard(supplier, BattleSideEnum.Attacker, broker.Object);

        guard.EarlyStart();

        Assert.True(guard.ReservePresent);
        broker.Verify(b => b.Publish(guard, It.IsAny<SendInformationMessage>()), Times.Never);
    }

    private static CoopNavalSetupGuard CreateGuard(Mock<IMessageBroker> broker) =>
        new CoopNavalSetupGuard(CreateSupplier(), BattleSideEnum.Attacker, broker.Object);

    private static CoopTroopSupplier CreateSupplier() =>
        new CoopTroopSupplier("battle", BattleSideEnum.Attacker, Mock.Of<IObjectManager>(), Mock.Of<IBattleAgentBudget>());
}
#endif
