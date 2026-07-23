using Common.Messaging;
using E2E.Tests.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.MapEventSides.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

public class MapEventLifetimeTests : MapEventTestBase
{
    public MapEventLifetimeTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void ServerCreate_MapEvent_SyncAllClients()
    {
        // Act
        var mapEventCtx = CreateServerMapEvent();

        // Assert
        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventCtx.MapEventId, out _));
        }
    }

    [Fact]
    public void ClientCreate_MapEvent_DoesNothing()
    {
        // Arrange
        var firstClient = Clients.First();
        string? mapEventId = null;

        // Act — clients must not be able to authoritatively create MapEvents
        firstClient.Call(() =>
        {
            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            Assert.False(firstClient.ObjectManager.TryGetId(mapEvent, out mapEventId));
        }, MapEventDisabledMethods);

        // Assert
        Assert.Null(mapEventId);
        Assert.False(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId ?? string.Empty, out _));
    }

    [Fact]
    public void ServerDestroy_MapEvent_SyncAllClients()
    {
        // Arrange
        var mapEventCtx = CreateServerMapEvent();

        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventCtx.MapEventId, out _));
        }

        // Act
        DestroyServerMapEvent(mapEventCtx.MapEventId);

        // Assert
        foreach (var client in Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<MapEvent>(mapEventCtx.MapEventId, out _));
        }
    }

    [Fact]
    public void ClientFinalize_AlreadyFinalizedMapEventDoesNotSendAnotherRequest()
    {
        var mapEventCtx = CreateServerMapEvent();
        var firstClient = Clients.First();
        firstClient.NetworkSentMessages.Clear();

        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MapEvent>(mapEventCtx.MapEventId, out var mapEvent));
            mapEvent.State = MapEventState.WaitingRemoval;
            mapEvent.FinalizeEvent();
        }, MapEventDisabledMethods);

        Assert.Empty(firstClient.NetworkSentMessages.GetMessages<NetworkMapEventFinalizeAttempted>());
        Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventCtx.MapEventId, out _));
    }

    [Fact]
    public void DefenderVictorySiege_FinalizesOnceAfterSideCleanup()
    {
        string? mapEventId = null;
        MapEvent? mapEvent = null;
        MobileParty? attacker = null;
        PartyBase? defender = null;
        MobileParty? mainPartyStandIn = null;
        var disabledMethods = MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(MobileParty), "OnPartyJoinedSiegeInternal"))
            .Append(AccessTools.Method(typeof(BesiegerCamp), nameof(BesiegerCamp.InitializeSiegeEventSide)))
            .Append(AccessTools.Method(typeof(Settlement), nameof(Settlement.InitializeSiegeEventSide)))
            .Append(AccessTools.Method(typeof(MapEvent), "ControlAndUpdateDefeatedPartiesAfterBattle"))
            .ToList();

        Server.Call(() =>
        {
            attacker = GameObjectCreator.CreateInitializedObject<MobileParty>();
            mainPartyStandIn = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var settlement = GameObjectCreator.CreateInitializedObject<Settlement>();
            var siegeEvent = new SiegeEvent(settlement, attacker);

            siegeEvent.BesiegerCamp._besiegerParties.Add(attacker);
            siegeEvent.BesiegerCamp._leaderParty = attacker;
            siegeEvent.BesiegerCamp._faction = attacker.MapFaction;

            mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            mapEvent._mapEventType = MapEvent.BattleTypes.Siege;
            mapEvent.MapEventSettlement = settlement;

            var defenderSide = new MapEventSide(mapEvent, BattleSideEnum.Defender, settlement.Party);
            var attackerSide = new MapEventSide(mapEvent, BattleSideEnum.Attacker, attacker.Party);
            mapEvent._sides[(int)BattleSideEnum.Defender] = defenderSide;
            mapEvent._sides[(int)BattleSideEnum.Attacker] = attackerSide;
            MessageBroker.Instance.Publish(mapEvent,
                new MapEventSideAssigned(mapEvent, defenderSide, BattleSideEnum.Defender));
            MessageBroker.Instance.Publish(mapEvent,
                new MapEventSideAssigned(mapEvent, attackerSide, BattleSideEnum.Attacker));

            attacker.Party.MapEventSide = attackerSide;
            settlement.Party.MapEventSide = defenderSide;
            mapEvent._battleState = BattleState.DefenderVictory;
            defender = settlement.Party;

            Campaign.Current.MapEventManager.OnMapEventCreated(mapEvent);
            Assert.True(Server.ObjectManager.TryGetId(mapEvent, out mapEventId));
        }, disabledMethods);

        Assert.NotNull(mapEventId);
        Server.InternalMessages.Clear();
        Server.NetworkSentMessages.Clear();

        Server.Call(() =>
        {
            var previousMainParty = Campaign.Current.MainParty;
            Campaign.Current.MainParty = mainPartyStandIn;
            try
            {
                mapEvent!.FinalizeEventAux();
            }
            finally
            {
                Campaign.Current.MainParty = previousMainParty;
            }

            Assert.All(mapEvent._sides, side => Assert.Empty(side.Parties));
            Assert.Null(attacker!.Party.MapEventSide);
            Assert.Null(defender!.MapEventSide);
            Assert.False(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId!, out _));
        }, disabledMethods);

        Assert.Single(Server.InternalMessages.GetMessages<InstanceDestroyed<MapEvent>>());
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkDestroyInstance<MapEvent>>());
        foreach (var client in Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<MapEvent>(mapEventId, out _));
        }
    }

    [Fact]
    public void ClientFinalize_MapEvent_ServerAuthoritativelyDestroys()
    {
        // Arrange
        var mapEventCtx = CreateServerMapEvent();
        var firstClient = Clients.First();

        // Act — a client cannot finalize locally: FinalizeEvent is intercepted and forwarded to the server
        // as a request, which finalizes the battle authoritatively and replicates the removal to every peer.
        firstClient.Call(() =>
        {
            Assert.True(firstClient.ObjectManager.TryGetObject<MapEvent>(mapEventCtx.MapEventId, out var mapEvent));
            mapEvent.FinalizeEvent();
        }, MapEventDisabledMethods);

        // Assert — the server honored the request and the destroy replicated everywhere
        Assert.False(Server.ObjectManager.TryGetObject<MapEvent>(mapEventCtx.MapEventId, out _));

        foreach (var client in Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<MapEvent>(mapEventCtx.MapEventId, out _));
        }
    }
}
