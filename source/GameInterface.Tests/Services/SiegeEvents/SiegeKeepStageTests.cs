using Common.Network;
using Common.Util;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.SiegeEvents;
using Moq;
using System;
using TaleWorlds.CampaignSystem.MapEvents;
using Xunit;

namespace GameInterface.Tests.Services.SiegeEvents;

public class SiegeKeepStageTests
{
    // Vanilla's default hall model: 0.7 attackers per defender, at most 19.
    [Theory]
    [InlineData(30, new[] { 10, 3 }, new[] { 10, 3 })] // 19 places, more than both parties hold
    [InlineData(10, new[] { 10, 3 }, new[] { 3, 2 })]  // 7 places, 5 escorts shared in seat order
    [InlineData(10, new[] { 1, 10 }, new[] { 1, 4 })]  // a seat with no troops left stops taking places
    [InlineData(1, new[] { 10, 3 }, new[] { 0 })]      // one place seats only the first player
    public void PlanSeats_SharesTheHallPlacesLeftAfterTheHeroes(int defenders, int[] escortsPerCandidate, int[] expected)
    {
        Assert.Equal(expected, SiegeKeepStage.PlanSeats(defenders, 0.7f, 19, escortsPerCandidate));
    }

    // A stage nobody answered stays open until its selection window closes on the stage's clock, then ends.
    [Fact]
    public void Tick_EndsAStageOnlyOnceItsSelectionWindowCloses()
    {
        var mapEvent = ObjectHelper.SkipConstructor<MapEvent>();
        var deadline = new DateTime(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);
        var now = deadline - TimeSpan.FromSeconds(1);
        var selection = new Mock<ISiegeKeepTroopSelection>();
        selection.Setup(store => store.GetOpenMapEvents()).Returns(new[] { mapEvent });
        selection.Setup(store => store.GetSeats(mapEvent)).Returns(Array.Empty<SiegeKeepSeat>());
        selection.Setup(store => store.TryGetDeadline(mapEvent, out deadline)).Returns(true);
        var stage = new SiegeKeepStage(Mock.Of<INetwork>(), Mock.Of<IObjectManager>(), Mock.Of<IPlayerManager>(),
            selection.Object, Mock.Of<IBattleTroopReserveBuilder>(), Mock.Of<ITroopSelectionEntries>(), () => now);

        stage.Tick();
        selection.Verify(store => store.Forget(mapEvent), Times.Never);

        now = deadline;
        stage.Tick();
        selection.Verify(store => store.Forget(mapEvent), Times.Once);
    }
}
