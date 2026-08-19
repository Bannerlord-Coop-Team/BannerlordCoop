using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Patches;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using GameInterface.Tests.Bootstrap;
using GameInterface.Tests.Services.SiegeEvents;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using Xunit;
using FormatterServices = System.Runtime.Serialization.FormatterServices;

namespace GameInterface.Tests.Services.MapEvents;

[Collection(nameof(CampaignCurrentCollection))]
public sealed class NearbyPartyReinforcerTests : IDisposable
{
    static NearbyPartyReinforcerTests()
    {
        GameBootStrap.Initialize();
    }

    private static readonly FieldInfo SidesField =
        typeof(MapEvent).GetField("_sides", BindingFlags.NonPublic | BindingFlags.Instance)!;
    private static readonly FieldInfo BattlePartiesField =
        typeof(MapEventSide).GetField("_battleParties", BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly ConditionalWeakTable<object, ControlledObjectInfo> playerObjects =
        (ConditionalWeakTable<object, ControlledObjectInfo>)AccessTools
            .Field(typeof(PlayerManager), "PlayerObjects")
            .GetValue(null)!;
    private readonly List<object> controlledObjects = new();

    public void Dispose()
    {
        foreach (var controlledObject in controlledObjects)
            playerObjects.Remove(controlledObject);
    }

    [Fact]
    public void PlayerBattleStart_ImmediatelyAddsSelectedNearbyAlly()
    {
        var playerParty = CreateMobileParty();
        var enemyParty = CreateMobileParty();
        var nearbyAlly = CreateMobileParty();
        var mapEvent = CreatePlayerBattle(playerParty, enemyParty);
        MarkAsPlayerParty(playerParty);
        InteractionPatches.OpenAiJoinWindowAndPublish(mapEvent, () => { });
        var joins = new List<(MapEventSide Side, MobileParty Party)>();
        var reinforcer = new NearbyPartyReinforcer();
        var previousMainParty = Campaign.Current.MainParty;
        var previousEncounter = Campaign.Current.PlayerEncounter;

        reinforcer.Reinforce(
            mapEvent,
            (playerSide, enemySide) =>
            {
                Assert.Same(playerParty, MobileParty.MainParty);
                Assert.Same(mapEvent, PlayerEncounter.Battle);
                playerSide.Add(nearbyAlly);
            },
            (side, party) => joins.Add((side, party)));

        Assert.Same(previousMainParty, Campaign.Current.MainParty);
        Assert.Same(previousEncounter, Campaign.Current.PlayerEncounter);
        var join = Assert.Single(joins);
        Assert.Same(mapEvent.AttackerSide, join.Side);
        Assert.Same(nearbyAlly, join.Party);
    }

    [Fact]
    public void CampaignTick_AddsAllyThatArrivesLaterDuringWindow()
    {
        var playerParty = CreateMobileParty();
        var enemyParty = CreateMobileParty();
        var lateAlly = CreateMobileParty();
        var mapEvent = CreatePlayerBattle(playerParty, enemyParty);
        MarkAsPlayerParty(playerParty);
        InteractionPatches.OpenAiJoinWindowAndPublish(mapEvent, () => { });
        var joins = new List<MobileParty>();
        var reinforcer = new NearbyPartyReinforcer();

        reinforcer.Reinforce(
            mapEvent,
            (attackers, defenders) => { },
            (side, party) => joins.Add(party));
        reinforcer.Reinforce(
            mapEvent,
            (attackers, defenders) => attackers.Add(lateAlly),
            (side, party) => joins.Add(party));

        Assert.Collection(joins, party => Assert.Same(lateAlly, party));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OutsideWindowOrFinalizedEvent_DoesNotAddJoiner(bool finalized)
    {
        var playerParty = CreateMobileParty();
        var enemyParty = CreateMobileParty();
        var nearbyAlly = CreateMobileParty();
        var mapEvent = CreatePlayerBattle(playerParty, enemyParty);
        MarkAsPlayerParty(playerParty);
        if (finalized)
        {
            InteractionPatches.OpenAiJoinWindowAndPublish(mapEvent, () => { });
            mapEvent._state = MapEventState.WaitingRemoval;
        }
        var selectorCalled = false;
        var joins = new List<MobileParty>();
        var reinforcer = new NearbyPartyReinforcer();

        reinforcer.Reinforce(
            mapEvent,
            (attackers, defenders) =>
            {
                selectorCalled = true;
                attackers.Add(nearbyAlly);
            },
            (side, party) => joins.Add(party));

        Assert.False(selectorCalled);
        Assert.Empty(joins);
    }

    [Fact]
    public void ConcludedEvent_DoesNotSelectJoiners()
    {
        var playerParty = CreateMobileParty();
        var enemyParty = CreateMobileParty();
        var mapEvent = CreatePlayerBattle(playerParty, enemyParty);
        MarkAsPlayerParty(playerParty);
        InteractionPatches.OpenAiJoinWindowAndPublish(mapEvent, () => { });
        mapEvent._battleState = TaleWorlds.Core.BattleState.AttackerVictory;
        var selectorCalled = false;
        var reinforcer = new NearbyPartyReinforcer();

        reinforcer.Reinforce(
            mapEvent,
            (playerSide, enemySide) => selectorCalled = true,
            (side, party) => { });

        Assert.False(selectorCalled);
    }

    [Fact]
    public void PlayerBattleAnnouncement_ObservesOpenAiJoinWindow()
    {
        var mapEvent = (MapEvent)FormatterServices.GetUninitializedObject(typeof(MapEvent));
        var published = false;

        InteractionPatches.OpenAiJoinWindowAndPublish(mapEvent, () =>
        {
            published = true;
            Assert.True(InteractionPatches.IsWithinAiJoinWindow(mapEvent));
        });

        Assert.True(published);
    }

    private MapEvent CreatePlayerBattle(MobileParty playerParty, MobileParty enemyParty)
    {
        var attackerSide = CreateSide(CreateMapEventParty(playerParty));
        var defenderSide = CreateSide(CreateMapEventParty(enemyParty));
        var mapEvent = (MapEvent)FormatterServices.GetUninitializedObject(typeof(MapEvent));
        SidesField.SetValue(mapEvent, new[] { defenderSide, attackerSide });
        return mapEvent;
    }

    private static MapEventSide CreateSide(params MapEventParty[] parties)
    {
        var side = (MapEventSide)FormatterServices.GetUninitializedObject(typeof(MapEventSide));
        BattlePartiesField.SetValue(side, new MBList<MapEventParty>(parties));
        return side;
    }

    private static MapEventParty CreateMapEventParty(MobileParty mobileParty)
    {
        var mapEventParty = (MapEventParty)FormatterServices.GetUninitializedObject(typeof(MapEventParty));
        mapEventParty.Party = mobileParty.Party;
        return mapEventParty;
    }

    private static MobileParty CreateMobileParty()
    {
        var mobileParty = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
        var party = (PartyBase)FormatterServices.GetUninitializedObject(typeof(PartyBase));
        party.MobileParty = mobileParty;
        mobileParty.Party = party;
        return mobileParty;
    }

    private void MarkAsPlayerParty(MobileParty mobileParty)
    {
        playerObjects.Add(
            mobileParty,
            new ControlledObjectInfo("PlayerOne", new ControllerIdProvider()));
        controlledObjects.Add(mobileParty);
    }
}
