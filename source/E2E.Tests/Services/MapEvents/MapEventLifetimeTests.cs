using Common.Messaging;
using Common.Network;
using Common.Util;
using E2E.Tests.Util;
using GameInterface.Registry.Auto;
using GameInterface.Services.MapEventSides.Messages;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

public class MapEventLifetimeTests : MapEventTestBase
{
    private static MobileParty? dispatchedSiegeLeader;

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
    public void SallyOutBesiegerLeader_RetreatAndRejoin_RestoresLeadershipBeforeFinalization()
    {
        const string controllerId = "sally-out-besieger";
        var client = Clients.First();
        var (_, besiegerMobilePartyId) = CreatePlayerHeroParty(controllerId);
        TestEnvironment.ConnectRegisteredPlayer(client, controllerId);
        var replacementMobilePartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var sallyingMobilePartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        string? mapEventId = null;
        string? besiegerPartyId = null;

        var disabledMethods = MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(MobileParty), "OnPartyJoinedSiegeInternal"))
            .Append(AccessTools.Method(typeof(BesiegerCamp), nameof(BesiegerCamp.InitializeSiegeEventSide)))
            .Append(AccessTools.Method(typeof(Settlement), nameof(Settlement.InitializeSiegeEventSide)))
            .Append(AccessTools.Method(typeof(MapEvent), "CommitCalculatedMapEventResults"))
            .ToList();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(besiegerMobilePartyId, out var besieger));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(replacementMobilePartyId, out var replacement));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(sallyingMobilePartyId, out var sallyingParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            var siegeEvent = new SiegeEvent(settlement, besieger);
            siegeEvent.BesiegerCamp._besiegerParties.Add(besieger);
            siegeEvent.BesiegerCamp._leaderParty = besieger;
            siegeEvent.BesiegerCamp._faction = besieger.MapFaction;

            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            mapEvent._mapEventType = MapEvent.BattleTypes.SallyOut;
            mapEvent.MapEventSettlement = settlement;
            var attackerSide = new MapEventSide(mapEvent, BattleSideEnum.Attacker, sallyingParty.Party);
            var defenderSide = new MapEventSide(mapEvent, BattleSideEnum.Defender, besieger.Party);
            mapEvent._sides[(int)BattleSideEnum.Attacker] = attackerSide;
            mapEvent._sides[(int)BattleSideEnum.Defender] = defenderSide;
            MessageBroker.Instance.Publish(mapEvent,
                new MapEventSideAssigned(mapEvent, attackerSide, BattleSideEnum.Attacker));
            MessageBroker.Instance.Publish(mapEvent,
                new MapEventSideAssigned(mapEvent, defenderSide, BattleSideEnum.Defender));

            sallyingParty.Party.MapEventSide = attackerSide;
            besieger.Party.MapEventSide = defenderSide;
            replacement.Party.MapEventSide = defenderSide;
            Campaign.Current.MapEventManager.OnMapEventCreated(mapEvent);

            Assert.True(Server.ObjectManager.TryGetId(mapEvent, out mapEventId));
            Assert.True(Server.ObjectManager.TryGetId(besieger.Party, out besiegerPartyId));
            Assert.Same(besieger.Party, defenderSide.LeaderParty);
        }, disabledMethods);

        Assert.NotNull(mapEventId);
        Assert.NotNull(besiegerPartyId);

        client.Call(() => client.Resolve<INetwork>().SendAll(
            new NetworkRequestLeaveBattle(besiegerPartyId!)), disabledMethods);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId!, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(replacementMobilePartyId, out var replacement));
            Assert.Same(replacement.Party, mapEvent.DefenderSide.LeaderParty);
        }, disabledMethods);

        client.Call(() => client.Resolve<INetwork>().SendAll(
            new NetworkRequestJoinBattle(
                Guid.NewGuid().ToString(),
                mapEventId!,
                besiegerPartyId!,
                BattleSideEnum.Defender)), disabledMethods);

        foreach (var instance in Clients.Prepend(Server))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MapEvent>(mapEventId!, out var mapEvent));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(besiegerMobilePartyId, out var besieger));
                Assert.Same(besieger.Party, mapEvent.DefenderSide.LeaderParty);
                Assert.Same(besieger.MapFaction, mapEvent.DefenderSide._mapFaction);
                Assert.Equal(
                    besieger.Party.LeaderHero?.PowerModifier ?? 0f,
                mapEvent.DefenderSide.LeaderSimulationModifier);
            }, disabledMethods);
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId!, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(besiegerMobilePartyId, out var besieger));
            mapEvent.ApplyGainedVariablesOnPlayerBattleContinues();
            Assert.Same(besieger.Party, mapEvent.DefenderSide.LeaderParty);
        }, disabledMethods);
    }

    [Fact]
    public void SallyOutBesiegerLeader_StaleAtFinalization_FinalizesAndReleasesParties()
    {
        var besiegerMobilePartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var replacementMobilePartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var sallyingMobilePartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        string? mapEventId = null;
        var disabledMethods = MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(MobileParty), "OnPartyJoinedSiegeInternal"))
            .Append(AccessTools.Method(typeof(BesiegerCamp), nameof(BesiegerCamp.InitializeSiegeEventSide)))
            .Append(AccessTools.Method(typeof(Settlement), nameof(Settlement.InitializeSiegeEventSide)))
            .Append(AccessTools.Method(typeof(MapEvent), "ControlAndUpdateDefeatedPartiesAfterBattle"))
            .Append(AccessTools.Method(typeof(DefaultBattleRewardModel), nameof(DefaultBattleRewardModel.GetCaptureMemberChancesForWinnerParties)))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyCasualties"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyItems"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyPrisoners"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyShips"))
            .Append(AccessTools.Method(typeof(MapEvent), "CalculateMapEventResults"))
            .Append(AccessTools.Method(typeof(MapEvent), "CommitCalculatedMapEventResults"))
            .Append(AccessTools.Method(typeof(MapEvent), "CaptureDefeatedPartyMembers"))
            .Append(AccessTools.Method(typeof(MapEvent), "MovePartyToSuitablePositionOnMapEventFinalize"))
            .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ExitToLast)))
            .Append(AccessTools.Method(typeof(MapEventRegistry), "CloseDestroyedMapEventEncounterIfNeeded"))
            .Append(AccessTools.Method(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.AfterSiegeCompleted)))
            .ToList();
        var harmony = new Harmony($"issue-2922-siege-completed-{Guid.NewGuid():N}");

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(besiegerMobilePartyId, out var besieger));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(replacementMobilePartyId, out var replacement));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(sallyingMobilePartyId, out var sallyingParty));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            var siegeEvent = new SiegeEvent(settlement, besieger);
            siegeEvent.BesiegerCamp._besiegerParties.Add(besieger);
            siegeEvent.BesiegerCamp._leaderParty = besieger;
            siegeEvent.BesiegerCamp._faction = besieger.MapFaction;

            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            mapEvent._mapEventType = MapEvent.BattleTypes.SallyOut;
            mapEvent.MapEventSettlement = settlement;
            var attackerSide = new MapEventSide(mapEvent, BattleSideEnum.Attacker, sallyingParty.Party);
            var defenderSide = new MapEventSide(mapEvent, BattleSideEnum.Defender, besieger.Party);
            mapEvent._sides[(int)BattleSideEnum.Attacker] = attackerSide;
            mapEvent._sides[(int)BattleSideEnum.Defender] = defenderSide;
            MessageBroker.Instance.Publish(mapEvent,
                new MapEventSideAssigned(mapEvent, attackerSide, BattleSideEnum.Attacker));
            MessageBroker.Instance.Publish(mapEvent,
                new MapEventSideAssigned(mapEvent, defenderSide, BattleSideEnum.Defender));

            sallyingParty.Party.MapEventSide = attackerSide;
            besieger.Party.MapEventSide = defenderSide;
            replacement.Party.MapEventSide = defenderSide;
            defenderSide.LeaderParty = replacement.Party;
            defenderSide._mapFaction = replacement.MapFaction;
            defenderSide.CacheLeaderSimulationModifier();
            Campaign.Current.MapEventManager.OnMapEventCreated(mapEvent);

            Assert.True(Server.ObjectManager.TryGetId(mapEvent, out mapEventId));
            Assert.Same(replacement.Party, defenderSide.LeaderParty);
        }, disabledMethods);

        Assert.NotNull(mapEventId);
        Server.InternalMessages.Clear();
        Server.NetworkSentMessages.Clear();
        dispatchedSiegeLeader = null;

        try
        {
            harmony.Patch(
                AccessTools.Method(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.SiegeCompleted)),
                prefix: new HarmonyMethod(typeof(MapEventLifetimeTests), nameof(RecordSiegeCompleted)));
            Server.Call(() => MessageBroker.Instance.Publish(
                this,
                new AuthoritativeBattleConclusionRequested(
                    mapEventId!,
                    BattleState.DefenderVictory,
                    hostEpoch: 0)), disabledMethods);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }

        Assert.False(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId!, out _));
        Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(besiegerMobilePartyId, out var expectedBesieger));
        Assert.Same(expectedBesieger, dispatchedSiegeLeader);
        Assert.Single(Server.InternalMessages.GetMessages<InstanceDestroyed<MapEvent>>());
        Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkDestroyInstance<MapEvent>>());

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(besiegerMobilePartyId, out var besieger));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(replacementMobilePartyId, out var replacement));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(sallyingMobilePartyId, out var sallyingParty));
            Assert.Null(besieger.Party.MapEventSide);
            Assert.Null(replacement.Party.MapEventSide);
            Assert.Null(sallyingParty.Party.MapEventSide);
        }, disabledMethods);

        foreach (var client in Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<MapEvent>(mapEventId!, out _));
        }
    }

    [Fact]
    public void SiegeCaptureLeader_StaleAtFinalization_IsRestoredBeforeCloseExclusion()
    {
        var (besiegerHeroId, besiegerMobilePartyId) = CreatePlayerHeroParty("siege-capture-leader");
        var replacementMobilePartyId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var settlementId = TestEnvironment.CreateRegisteredObject<Settlement>();
        string? besiegerPartyId = null;
        string[]? partiesToClose = null;
        var disabledMethods = MapEventDisabledMethods
            .Append(AccessTools.Method(typeof(MobileParty), "OnPartyJoinedSiegeInternal"))
            .Append(AccessTools.Method(typeof(BesiegerCamp), nameof(BesiegerCamp.InitializeSiegeEventSide)))
            .Append(AccessTools.Method(typeof(Settlement), nameof(Settlement.InitializeSiegeEventSide)))
            .Append(AccessTools.Method(typeof(MapEvent), "ControlAndUpdateDefeatedPartiesAfterBattle"))
            .Append(AccessTools.Method(typeof(DefaultBattleRewardModel), nameof(DefaultBattleRewardModel.GetCaptureMemberChancesForWinnerParties)))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyCasualties"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyItems"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyPrisoners"))
            .Append(AccessTools.Method(typeof(MapEvent), "LootDefeatedPartyShips"))
            .Append(AccessTools.Method(typeof(MapEvent), "CalculateMapEventResults"))
            .Append(AccessTools.Method(typeof(MapEvent), "CommitCalculatedMapEventResults"))
            .Append(AccessTools.Method(typeof(MapEvent), "CaptureDefeatedPartyMembers"))
            .Append(AccessTools.Method(typeof(MapEvent), "MovePartyToSuitablePositionOnMapEventFinalize"))
            .Append(AccessTools.Method(typeof(GameMenu), nameof(GameMenu.ExitToLast)))
            .Append(AccessTools.Method(typeof(MapEventRegistry), "CloseDestroyedMapEventEncounterIfNeeded"))
            .Append(AccessTools.Method(typeof(CampaignEventDispatcher), nameof(CampaignEventDispatcher.AfterSiegeCompleted)))
            .ToList();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<Hero>(besiegerHeroId, out var besiegerHero));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(besiegerMobilePartyId, out var besieger));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(replacementMobilePartyId, out var replacement));
            Assert.True(Server.ObjectManager.TryGetObject<Settlement>(settlementId, out var settlement));

            besiegerHero.PartyBelongedTo = besieger;
            besieger.LordPartyComponent._leader = besiegerHero;

            var siegeEvent = new SiegeEvent(settlement, besieger);
            siegeEvent.BesiegerCamp._besiegerParties.Add(besieger);
            siegeEvent.BesiegerCamp._leaderParty = besieger;
            siegeEvent.BesiegerCamp._faction = besieger.MapFaction;

            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            mapEvent._mapEventType = MapEvent.BattleTypes.Siege;
            mapEvent.MapEventSettlement = settlement;
            var defenderSide = new MapEventSide(mapEvent, BattleSideEnum.Defender, settlement.Party);
            var attackerSide = new MapEventSide(mapEvent, BattleSideEnum.Attacker, besieger.Party);
            mapEvent._sides[(int)BattleSideEnum.Defender] = defenderSide;
            mapEvent._sides[(int)BattleSideEnum.Attacker] = attackerSide;
            MessageBroker.Instance.Publish(mapEvent,
                new MapEventSideAssigned(mapEvent, defenderSide, BattleSideEnum.Defender));
            MessageBroker.Instance.Publish(mapEvent,
                new MapEventSideAssigned(mapEvent, attackerSide, BattleSideEnum.Attacker));

            besieger.Party.MapEventSide = attackerSide;
            settlement.Party.MapEventSide = defenderSide;
            replacement.Party.MapEventSide = attackerSide;
            attackerSide.LeaderParty = replacement.Party;
            attackerSide._mapFaction = replacement.MapFaction;
            attackerSide.CacheLeaderSimulationModifier();
            mapEvent._battleState = BattleState.AttackerVictory;
            Campaign.Current.MapEventManager.OnMapEventCreated(mapEvent);

            Assert.True(Server.ObjectManager.TryGetId(besieger.Party, out besiegerPartyId));
            Assert.Same(replacement.Party, attackerSide.LeaderParty);

            var handler = Server.Resolve<BattleFinalizeHandler>();
            partiesToClose = (string[])AccessTools.Method(
                typeof(BattleFinalizeHandler),
                "FinalizeAndCollectPlayers").Invoke(handler, new object?[] { mapEvent, null })!;
        }, disabledMethods);

        Assert.NotNull(besiegerPartyId);
        Assert.NotNull(partiesToClose);
        Assert.DoesNotContain(besiegerPartyId!, partiesToClose!);
    }

    private static bool RecordSiegeCompleted(MobileParty attackerParty)
    {
        dispatchedSiegeLeader = attackerParty;
        return false;
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
