using Common.Network;
using Common.Util;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.SiegeEvents;
using GameInterface.Services.Hideouts;
using GameInterface.Services.Players.Data;
using HarmonyLib;
using Moq;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
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

    // The last pick starts the lock, so a lock that fails and closes the stage has to refuse that pick, otherwise the
    // player is told to wait for a lock that never comes.
    [Fact]
    public void LastPickWhoseLockFails_IsRefusedAndClosesTheStage()
    {
        var mapEvent = ObjectHelper.SkipConstructor<MapEvent>();
        var party = PartyIn(mapEvent);
        var partyId = "party";
        var player = new Player("player", "hero-object", partyId, "clan", "hero");
        var objectManager = new Mock<IObjectManager>();
        objectManager.Setup(manager => manager.TryGetId(party, out partyId)).Returns(true);
        objectManager.Setup(manager => manager.TryGetObject(partyId, out party)).Returns(true);
        var players = new Mock<IPlayerManager>();
        players.Setup(manager => manager.TryGetPlayer("player", out player)).Returns(true);
        players.Setup(manager => manager.IsConnected(player)).Returns(true);
        var accepted = new[] { new HideoutTroopSelectionEntry("hero", 1) };
        var escorts = 0;
        var rules = new Mock<ITroopSelectionRules>();
        rules.Setup(rule => rule.CountHealthy(party, "hero")).Returns(new Dictionary<string, int> { ["hero"] = 1 });
        rules.Setup(rule => rule.TryAccept(It.IsAny<IReadOnlyDictionary<string, int>>(), "hero", 0,
            It.IsAny<IReadOnlyList<HideoutTroopSelectionEntry>>(), out accepted, out escorts)).Returns(true);
        var reserves = new Mock<IBattleTroopReserveBuilder>();
        reserves.Setup(builder => builder.ForgetMapEvent(mapEvent, true)).Throws(new InvalidOperationException("reserves failed"));
        var store = new SiegeKeepTroopSelection(objectManager.Object, rules.Object);
        store.Open(mapEvent, new[] { new SiegeKeepSeat("player", partyId, "hero", 0) }, new Dictionary<string, int>(),
            DateTime.UtcNow.AddSeconds(90));
        var stage = new SiegeKeepStage(Mock.Of<INetwork>(), objectManager.Object, players.Object, store,
            reserves.Object, Mock.Of<ITroopSelectionEntries>());

        Assert.False(stage.TrySelect(mapEvent, party, accepted));
        Assert.False(store.IsOpen(mapEvent));
    }

    // A party fighting in the given map event.
    private static MobileParty PartyIn(MapEvent mapEvent)
    {
        var side = ObjectHelper.SkipConstructor<MapEventSide>();
        AccessTools.Field(typeof(MapEventSide), "_mapEvent").SetValue(side, mapEvent);
        var partyBase = ObjectHelper.SkipConstructor<PartyBase>();
        AccessTools.Field(typeof(PartyBase), "_mapEventSide").SetValue(partyBase, side);
        var party = ObjectHelper.SkipConstructor<MobileParty>();
        AccessTools.Property(typeof(MobileParty), nameof(MobileParty.Party)).SetValue(party, partyBase);
        return party;
    }
}
