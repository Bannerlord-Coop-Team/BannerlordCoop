#if COOP_NAVAL
using Common.Messaging;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using Missions.Battles;
using Missions.Naval;
using Moq;
using TaleWorlds.Core;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>R41a: a coop naval battle whose own reserve is missing at the naval spawn ends instead of hanging in deployment.</summary>
public class CoopNavalSetupGuardTests
{
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

    private static CoopTroopSupplier CreateSupplier() =>
        new CoopTroopSupplier("battle", BattleSideEnum.Attacker, Mock.Of<IObjectManager>(), Mock.Of<IBattleAgentBudget>());
}
#endif
