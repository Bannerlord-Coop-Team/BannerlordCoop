using GameInterface.Services.MapEvents;
using Common.Messaging;
using Coop.Core.Server.Services.Save.Messages;
using E2E.Tests.Util;
using GameInterface.Services.MapEvents.Messages.Leave;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

public class MapEventLoadCleanerTests : MapEventTestBase
{
    public MapEventLoadCleanerTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void FinalizePlayerMapEvents_OrphanPlayerEvent_ReleasesAndParksOfflineParty()
    {
        var mapEventContext = CreateServerMapEvent(commit: false);
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        RegisterAsPlayerParty("offline-player", heroId, mapEventContext.AttackerPartyId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.AttackerPartyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.DefenderPartyId, out var aiParty));
            Assert.DoesNotContain(mapEvent, Campaign.Current.MapEventManager.MapEvents);

            aiParty.PartyMoveMode = MoveModeType.Party;
            aiParty.MoveTargetParty = playerParty;
            aiParty.Ai.RethinkAtNextHourlyTick = false;

            Server.Resolve<IMessageBroker>().Publish(this, new SavedPlayerRegistrationsRestored());

            Assert.True(playerParty.IsActive);
            Assert.True(playerParty.IsVisible);
            Assert.Same(mapEvent, playerParty.MapEvent);

            Server.Resolve<IMapEventLoadCleaner>().FinalizePlayerMapEvents();

            Assert.Equal(MapEventState.WaitingRemoval, mapEvent.State);
            Assert.Null(playerParty.Party.MapEventSide);
            Assert.Null(aiParty.Party.MapEventSide);
            Assert.False(playerParty.IsActive);
            Assert.False(playerParty.IsVisible);
            Assert.Equal(MoveModeType.Hold, aiParty.PartyMoveMode);
            Assert.Null(aiParty.MoveTargetParty);
            Assert.True(aiParty.Ai.RethinkAtNextHourlyTick);
            Assert.False(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out _));
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void FinalizePlayerMapEvents_SharedOrphanPlayerEvent_FinalizesOnce()
    {
        var mapEventContext = CreateServerMapEvent(commit: false);
        RegisterAsPlayerParty(
            "attacking-player",
            TestEnvironment.CreateRegisteredObject<Hero>(),
            mapEventContext.AttackerPartyId);
        RegisterAsPlayerParty(
            "defending-player",
            TestEnvironment.CreateRegisteredObject<Hero>(),
            mapEventContext.DefenderPartyId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            var messageBroker = Server.Resolve<IMessageBroker>();
            var finalizedCount = 0;

            void CountFinalization(MessagePayload<MapEventFinalized> payload)
            {
                if (ReferenceEquals(payload.What.MapEvent, mapEvent))
                    finalizedCount++;
            }

            messageBroker.Subscribe<MapEventFinalized>(CountFinalization);
            try
            {
                Server.Resolve<IMapEventLoadCleaner>().FinalizePlayerMapEvents();
            }
            finally
            {
                messageBroker.Unsubscribe<MapEventFinalized>(CountFinalization);
            }

            Assert.Equal(1, finalizedCount);
            Assert.Equal(MapEventState.WaitingRemoval, mapEvent.State);
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void FinalizePlayerMapEvents_PlayerEvent_ReleasesPartiesAndDestroysReplicatedEvent()
    {
        var mapEventContext = CreateServerMapEvent();
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        RegisterAsPlayerParty("loaded-player", heroId, mapEventContext.AttackerPartyId);
        string? armyId = null;
        string? followerId = null;
        string? gatheringFollowerId = null;
        string? destinationId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.AttackerPartyId, out var attacker));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.DefenderPartyId, out var defender));
            var kingdom = GameObjectCreator.CreateInitializedObject<Kingdom>();
            var follower = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var gatheringFollower = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var destination = GameObjectCreator.CreateInitializedObject<Settlement>();
            destination.SetSettlementComponent(GameObjectCreator.CreateInitializedObject<Town>());
            destination._position = follower.Position;
            follower.SetCustomHomeSettlement(destination);
            gatheringFollower.SetCustomHomeSettlement(destination);
            var army = new Army(kingdom, attacker, Army.ArmyTypes.Raider);
            follower.Army = army;
            follower.AttachedTo = attacker;
            gatheringFollower.Army = army;
            follower.Ai.RethinkAtNextHourlyTick = false;
            Assert.True(Server.ObjectManager.TryGetId(army, out armyId));
            Assert.True(Server.ObjectManager.TryGetId(follower, out followerId));
            Assert.True(Server.ObjectManager.TryGetId(gatheringFollower, out gatheringFollowerId));
            Assert.True(Server.ObjectManager.TryGetId(destination, out destinationId));
            Assert.Same(mapEvent, follower.MapEvent);
            Assert.Null(gatheringFollower.MapEvent);

            Server.Resolve<IMapEventLoadCleaner>().FinalizePlayerMapEvents();
            TestEnvironment.FlushCoalescer();

            Assert.Equal(MapEventState.WaitingRemoval, mapEvent.State);
            Assert.Null(attacker.Party.MapEventSide);
            Assert.Null(defender.Party.MapEventSide);
            Assert.Null(follower.Party.MapEventSide);
            Assert.Null(follower.Army);
            Assert.Null(follower.AttachedTo);
            Assert.Equal(AiBehavior.GoToSettlement, follower.DefaultBehavior);
            Assert.Same(destination, follower.TargetSettlement);
            Assert.Null(gatheringFollower.Army);
            Assert.Null(gatheringFollower.AttachedTo);
            Assert.Equal(AiBehavior.GoToSettlement, gatheringFollower.DefaultBehavior);
            Assert.Same(destination, gatheringFollower.TargetSettlement);
            Assert.False(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out _));
            Assert.False(Server.ObjectManager.TryGetObject<Army>(armyId, out _));
        }, MapEventDisabledMethods);

        foreach (var client in Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out _));
            Assert.False(client.ObjectManager.TryGetObject<Army>(armyId, out _));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(followerId, out var follower));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(gatheringFollowerId, out var gatheringFollower));
            Assert.True(client.ObjectManager.TryGetObject<Settlement>(destinationId, out var destination));
            Assert.Null(follower.Army);
            Assert.Null(follower.AttachedTo);
            Assert.Equal(AiBehavior.GoToSettlement, follower.DefaultBehavior);
            Assert.Same(destination, follower.TargetSettlement);
            Assert.Null(gatheringFollower.Army);
            Assert.Null(gatheringFollower.AttachedTo);
            Assert.Equal(AiBehavior.GoToSettlement, gatheringFollower.DefaultBehavior);
            Assert.Same(destination, gatheringFollower.TargetSettlement);
        }
    }

    [Fact]
    public void FinalizePlayerMapEvents_PlayerFollowerInAiLedArmy_PreservesArmy()
    {
        var mapEventContext = CreateServerMapEvent();
        var (_, playerPartyId) = CreatePlayerHeroParty("loaded-player-follower");
        string? armyId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.AttackerPartyId, out var armyLeader));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerFollower));
            var kingdom = GameObjectCreator.CreateInitializedObject<Kingdom>();
            var army = new Army(kingdom, armyLeader, Army.ArmyTypes.Raider);
            playerFollower.Army = army;
            playerFollower.AttachedTo = armyLeader;
            Assert.True(Server.ObjectManager.TryGetId(army, out armyId));
            Assert.Same(mapEvent, playerFollower.MapEvent);

            Server.Resolve<IMapEventLoadCleaner>().FinalizePlayerMapEvents();

            Assert.False(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out _));
            Assert.True(Server.ObjectManager.TryGetObject<Army>(armyId, out _));
            Assert.Same(army, playerFollower.Army);
            Assert.Same(armyLeader, playerFollower.AttachedTo);
        }, MapEventDisabledMethods);

        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<Army>(armyId, out var army));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(playerPartyId, out var playerFollower));
            Assert.True(client.ObjectManager.TryGetObject<MobileParty>(mapEventContext.AttackerPartyId, out var armyLeader));
            Assert.Same(army, playerFollower.Army);
            Assert.Same(armyLeader, playerFollower.AttachedTo);
        }
    }

    [Fact]
    public void FinalizePlayerMapEvents_AiEvent_RemainsActive()
    {
        var mapEventContext = CreateServerMapEvent();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.AttackerPartyId, out var attacker));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.DefenderPartyId, out var defender));

            Server.Resolve<IMapEventLoadCleaner>().FinalizePlayerMapEvents();

            Assert.False(mapEvent.IsFinalized);
            Assert.Same(mapEvent, attacker.MapEvent);
            Assert.Same(mapEvent, defender.MapEvent);
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out _));
        }, MapEventDisabledMethods);

        foreach (var client in Clients)
        {
            Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out _));
        }
    }
}
