using GameInterface.Services.MapEvents;
using Common.Messaging;
using Common.Util;
using Coop.Core.Server.Services.Save.Messages;
using GameInterface.Registry;
using GameInterface.Services.Armies;
using GameInterface.Services.Players;
using Serilog;
using Serilog.Events;
using E2E.Tests.Util;
using GameInterface.Services.MapEvents.Messages.Leave;
using HarmonyLib;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

public class MapEventLoadCleanerTests : MapEventTestBase
{
    private static readonly AccessTools.FieldRef<MapEvent, TroopUpgradeTracker?> TrackerField =
        AccessTools.FieldRefAccess<MapEvent, TroopUpgradeTracker?>("<TroopUpgradeTracker>k__BackingField");

    private readonly ITestOutputHelper output;

    public MapEventLoadCleanerTests(ITestOutputHelper output) : base(output)
    {
        this.output = output;
    }

    private IReadOnlyList<MethodBase> JoinDisabledMethods => MapEventDisabledMethods
        .Append(AccessTools.Method(typeof(CultureObject), nameof(CultureObject.HasFeat)))
        .ToList();

    /// <summary>
    /// Models an event the save wrote while half finalized: WaitingRemoval, and none of the state the
    /// save skips (the tracker, the component's back-reference to the event and the field battle
    /// position lists), because the load only restores it for events the manager lists.
    /// </summary>
    private static void ModelOrphanedAfterLoad(MapEvent mapEvent)
    {
        mapEvent.State = MapEventState.WaitingRemoval;
        TrackerField(mapEvent) = null;
        var component = Assert.IsType<FieldBattleEventComponent>(mapEvent.Component);
        component.MapEvent = null!;
        component._validLocalPositionsForAttackerSide = null!;
        component._validLocalPositionsForDefenderSide = null!;
    }

    [Fact]
    public void CleanUpLoadedMapEvents_OrphanPlayerEvent_ReleasesAndParksOfflineParty()
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

            Server.Resolve<IMapEventLoadCleaner>().CleanUpLoadedMapEvents();

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
    public void CleanUpLoadedMapEvents_StaleParticipantSideLink_RepairsAndCompletes()
    {
        var mapEventContext = CreateServerMapEvent(commit: false);
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        RegisterAsPlayerParty("offline-player-stale-side", heroId, mapEventContext.AttackerPartyId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.DefenderPartyId, out var defender));
            Assert.Contains(mapEvent.DefenderSide.Parties, party => ReferenceEquals(party.Party, defender.Party));

            // Reproduce a malformed loaded graph: the side still owns its MapEventParty, but the
            // PartyBase backing link is gone. Vanilla HandleMapEventEnd otherwise loops forever
            // because assigning null to an already-null link never removes the side-list entry.
            defender.Party._mapEventSide = null;

            Server.Resolve<IMessageBroker>().Publish(this, new SavedPlayerRegistrationsRestored());
            Server.Resolve<IMapEventLoadCleaner>().CleanUpLoadedMapEvents();

            Assert.Equal(MapEventState.WaitingRemoval, mapEvent.State);
            Assert.Empty(mapEvent.AttackerSide.Parties);
            Assert.Empty(mapEvent.DefenderSide.Parties);
            Assert.Null(defender.Party.MapEventSide);
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void CleanUpLoadedMapEvents_CrossLinkedParticipantSide_RepairsAndCompletes()
    {
        var mapEventContext = CreateServerMapEvent(commit: false);
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        RegisterAsPlayerParty("offline-player-cross-linked-side", heroId, mapEventContext.AttackerPartyId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.DefenderPartyId, out var defender));
            Assert.Contains(mapEvent.DefenderSide.Parties, party => ReferenceEquals(party.Party, defender.Party));

            defender.Party._mapEventSide = mapEvent.AttackerSide;

            Server.Resolve<IMessageBroker>().Publish(this, new SavedPlayerRegistrationsRestored());
            Server.Resolve<IMapEventLoadCleaner>().CleanUpLoadedMapEvents();

            Assert.Equal(MapEventState.WaitingRemoval, mapEvent.State);
            Assert.Empty(mapEvent.AttackerSide.Parties);
            Assert.Empty(mapEvent.DefenderSide.Parties);
            Assert.Null(defender.Party.MapEventSide);
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void CleanUpLoadedMapEvents_DuplicateParticipantEntry_PrunesAndCompletes()
    {
        var mapEventContext = CreateServerMapEvent(commit: false);
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        RegisterAsPlayerParty("offline-player-duplicate-entry", heroId, mapEventContext.AttackerPartyId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.DefenderPartyId, out var defender));

            mapEvent.DefenderSide._battleParties.Add(new MapEventParty(defender.Party));
            Assert.Equal(2, mapEvent.DefenderSide.Parties.Count(party => ReferenceEquals(party.Party, defender.Party)));

            Server.Resolve<IMessageBroker>().Publish(this, new SavedPlayerRegistrationsRestored());
            Server.Resolve<IMapEventLoadCleaner>().CleanUpLoadedMapEvents();

            Assert.Equal(MapEventState.WaitingRemoval, mapEvent.State);
            Assert.Empty(mapEvent.AttackerSide.Parties);
            Assert.Empty(mapEvent.DefenderSide.Parties);
            Assert.Null(defender.Party.MapEventSide);
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void CleanUpLoadedMapEvents_PartyInAnotherEvent_PreservesAuthoritativeMembership()
    {
        var loadedEventContext = CreateServerMapEvent(commit: false);
        var activeEventContext = CreateServerMapEvent();
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        RegisterAsPlayerParty("offline-player-foreign-event", heroId, loadedEventContext.AttackerPartyId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(loadedEventContext.MapEventId, out var loadedEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(activeEventContext.MapEventId, out var activeEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(loadedEventContext.DefenderPartyId, out var defender));

            activeEvent.AttackerSide._battleParties.Add(new MapEventParty(defender.Party));
            defender.Party._mapEventSide = activeEvent.AttackerSide;

            Server.Resolve<IMessageBroker>().Publish(this, new SavedPlayerRegistrationsRestored());
            Server.Resolve<IMapEventLoadCleaner>().CleanUpLoadedMapEvents();

            Assert.Equal(MapEventState.WaitingRemoval, loadedEvent.State);
            Assert.DoesNotContain(loadedEvent.InvolvedParties, party => ReferenceEquals(party, defender.Party));
            Assert.Same(activeEvent.AttackerSide, defender.Party.MapEventSide);
            Assert.Contains(activeEvent.AttackerSide.Parties, party => ReferenceEquals(party.Party, defender.Party));
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void CleanUpLoadedMapEvents_UnregisteredPlayerPartyInCampaignObjectManager_FinalizesEvent()
    {
        var mapEventContext = CreateServerMapEvent(commit: false);
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        RegisterAsPlayerParty("offline-unregistered-player", heroId, mapEventContext.AttackerPartyId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.AttackerPartyId, out var playerParty));
            Assert.Contains(playerParty, Campaign.Current.CampaignObjectManager.MobileParties);

            Server.Resolve<IMessageBroker>().Publish(this, new SavedPlayerRegistrationsRestored());

            Assert.True(playerParty.IsActive);
            Assert.True(playerParty.IsVisible);
            Assert.True(Server.ObjectManager.Remove(playerParty));
            Assert.False(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.AttackerPartyId, out _));

            Server.Resolve<IMapEventLoadCleaner>().CleanUpLoadedMapEvents();

            Assert.Equal(MapEventState.WaitingRemoval, mapEvent.State);
            Assert.Null(playerParty.Party.MapEventSide);
            Assert.False(playerParty.IsActive);
            Assert.False(playerParty.IsVisible);
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void CleanUpLoadedMapEvents_HalfFinalizedOrphan_DetachesAndParksOfflineParty()
    {
        var mapEventContext = CreateServerMapEvent(commit: false);
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        RegisterAsPlayerParty("offline-half-finalized-player", heroId, mapEventContext.AttackerPartyId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.AttackerPartyId, out var playerParty));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.DefenderPartyId, out var aiParty));

            aiParty.PartyMoveMode = MoveModeType.Party;
            aiParty.MoveTargetParty = playerParty;
            aiParty.Ai.RethinkAtNextHourlyTick = false;
            Server.Resolve<IMessageBroker>().Publish(this, new SavedPlayerRegistrationsRestored());
            mapEvent.State = MapEventState.WaitingRemoval;

            Assert.True(mapEvent.IsFinalized);
            Assert.Same(mapEvent, playerParty.MapEvent);
            Assert.Same(mapEvent, aiParty.MapEvent);

            Server.Resolve<IMapEventLoadCleaner>().CleanUpLoadedMapEvents();

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
    public void CleanUpLoadedMapEvents_SharedOrphanPlayerEvent_FinalizesOnce()
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
                Server.Resolve<IMapEventLoadCleaner>().CleanUpLoadedMapEvents();
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
    public void CleanUpLoadedMapEvents_PlayerEvent_ReleasesPartiesAndDestroysReplicatedEvent()
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

            Server.Resolve<IMapEventLoadCleaner>().CleanUpLoadedMapEvents();
            TestEnvironment.FlushCoalescer();

            Assert.Equal(MapEventState.WaitingRemoval, mapEvent.State);
            Assert.Null(attacker.Party.MapEventSide);
            Assert.Null(defender.Party.MapEventSide);
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
    public void CleanUpLoadedMapEvents_PlayerFollowerInAiLedArmy_PreservesArmy()
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

            Server.Resolve<IMapEventLoadCleaner>().CleanUpLoadedMapEvents();

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
    public void CleanUpLoadedMapEvents_AiPartiesInUnregisteredEvent_ReleasesParties()
    {
        // a save where an AI battle already left the map event manager but its parties still link to it
        var mapEventContext = CreateServerMapEvent(commit: false);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.AttackerPartyId, out var attacker));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.DefenderPartyId, out var defender));
            Assert.DoesNotContain(mapEvent, Campaign.Current.MapEventManager.MapEvents);
            Assert.Same(mapEvent, attacker.MapEvent);

            attacker.PartyMoveMode = MoveModeType.Party;
            attacker.MoveTargetParty = defender;

            Server.Resolve<IMapEventLoadCleaner>().CleanUpLoadedMapEvents();

            Assert.Null(attacker.Party.MapEventSide);
            Assert.Null(defender.Party.MapEventSide);
            Assert.Equal(MoveModeType.Hold, attacker.PartyMoveMode);
            Assert.Null(attacker.MoveTargetParty);

            // the old battle keeps no participants and no network registration
            Assert.True(mapEvent.IsFinalized);
            Assert.Empty(mapEvent.AttackerSide.Parties);
            Assert.Empty(mapEvent.DefenderSide.Parties);
            Assert.Empty(mapEvent.InvolvedParties);
            Assert.False(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out _));
        }, MapEventDisabledMethods);

        // only the cleaner's own retirement, FinalizeEventAux does not run the ended battle again
        Assert.Single(Server.InternalMessages.GetMessages<MapEventFinalized>());
    }

    [Fact]
    public void CleanUpLoadedMapEvents_AiPartiesInFinalizedEvent_ReleasesParties()
    {
        // a save where an AI battle was already finalized but its parties still link to it
        var mapEventContext = CreateServerMapEvent();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.AttackerPartyId, out var attacker));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.DefenderPartyId, out var defender));
            mapEvent._state = MapEventState.WaitingRemoval;
            Assert.Same(mapEvent, attacker.MapEvent);

            Server.Resolve<IMapEventLoadCleaner>().CleanUpLoadedMapEvents();

            Assert.Null(attacker.Party.MapEventSide);
            Assert.Null(defender.Party.MapEventSide);
            Assert.Equal(MapEventState.WaitingRemoval, mapEvent.State);
            Assert.Empty(mapEvent.AttackerSide.Parties);
            Assert.Empty(mapEvent.DefenderSide.Parties);
            Assert.False(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out _));
        }, MapEventDisabledMethods);

        Assert.Single(Server.InternalMessages.GetMessages<MapEventFinalized>());

        foreach (var client in Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out _));
        }
    }

    [Fact]
    public void CleanUpLoadedMapEvents_AiEvent_RemainsActive()
    {
        var mapEventContext = CreateServerMapEvent();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.AttackerPartyId, out var attacker));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.DefenderPartyId, out var defender));

            Server.Resolve<IMapEventLoadCleaner>().CleanUpLoadedMapEvents();

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

    [Fact]
    public void CleanUpLoadedMapEvents_HalfFinalizedUnloadedEventWithTwoDefenders_Completes()
    {
        var mapEventContext = CreateServerMapEvent(commit: false);
        var heroId = TestEnvironment.CreateRegisteredObject<Hero>();
        RegisterAsPlayerParty("offline-unloaded-two-defenders", heroId, mapEventContext.AttackerPartyId);
        var extraDefenderId = JoinNewServerPartyToSide(mapEventContext.MapEventId, BattleSideEnum.Defender);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.DefenderPartyId, out var defender));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(extraDefenderId, out var extraDefender));
            Server.Resolve<IMessageBroker>().Publish(this, new SavedPlayerRegistrationsRestored());
            ModelOrphanedAfterLoad(mapEvent);

            Server.Resolve<IMapEventLoadCleaner>().CleanUpLoadedMapEvents();

            Assert.Null(defender.Party.MapEventSide);
            Assert.Null(extraDefender.Party.MapEventSide);
            Assert.Empty(mapEvent.AttackerSide.Parties);
            Assert.Empty(mapEvent.DefenderSide.Parties);
        }, JoinDisabledMethods);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CleanUpLoadedMapEvents_FinalizedUnloadedAiEvent_ReleasesStuckAndJoinedParties(bool originalsAllowed)
    {
        var mapEventContext = CreateServerMapEvent(commit: false);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.AttackerPartyId, out var attacker));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.DefenderPartyId, out var defender));
            Assert.DoesNotContain(mapEvent, Campaign.Current.MapEventManager.MapEvents);

            defender.PartyMoveMode = MoveModeType.Party;
            defender.MoveTargetParty = attacker;
            defender.Ai.RethinkAtNextHourlyTick = false;
            ModelOrphanedAfterLoad(mapEvent);

            // Baseline: a third party engaging the orphan hits the null position list, and the failed
            // join leaves it attached, so every failed encounter freezes one more party.
            var third = GameObjectCreator.CreateInitializedObject<MobileParty>();
            Assert.ThrowsAny<Exception>(() => third.Party.MapEventSide = mapEvent.AttackerSide);
            Assert.Same(mapEvent.AttackerSide, third.Party.MapEventSide);

            Assert.Equal(1, CountFinalizedMessages(mapEvent, () => RunRepair(originalsAllowed)));

            Assert.Null(attacker.Party.MapEventSide);
            Assert.Null(defender.Party.MapEventSide);
            Assert.Null(third.Party.MapEventSide);
            Assert.Empty(mapEvent.AttackerSide.Parties);
            Assert.Empty(mapEvent.DefenderSide.Parties);
            Assert.Equal(MapEventState.WaitingRemoval, mapEvent.State);
            Assert.Equal(MoveModeType.Hold, defender.PartyMoveMode);
            Assert.Null(defender.MoveTargetParty);
            Assert.True(defender.Ai.RethinkAtNextHourlyTick);

            // The repair never finalizes again, and a second pass finds nothing referencing the event.
            Assert.Equal(0, CountFinalizedMessages(mapEvent, () => RunRepair(originalsAllowed)));
        }, JoinDisabledMethods);
    }

    [Fact]
    public void CleanUpLoadedMapEvents_PartyLinkedToAnotherLiveEvent_IsNotReleasedOrReset()
    {
        var orphanContext = CreateServerMapEvent(commit: false);
        var liveContext = CreateServerMapEvent();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(orphanContext.MapEventId, out var orphan));
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(liveContext.MapEventId, out var live));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(orphanContext.AttackerPartyId, out var stuck));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(orphanContext.DefenderPartyId, out var moved));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(liveContext.AttackerPartyId, out var target));

            // The orphan still lists the defender, but the party now belongs to the live event.
            live.AttackerSide._battleParties.Add(new MapEventParty(moved.Party));
            moved.Party._mapEventSide = live.AttackerSide;
            moved.IsActive = true;
            moved.PartyMoveMode = MoveModeType.Party;
            moved.MoveTargetParty = target;
            moved.Ai.RethinkAtNextHourlyTick = false;
            ModelOrphanedAfterLoad(orphan);

            RunRepair(originalsAllowed: false);

            Assert.Null(stuck.Party.MapEventSide);
            Assert.Same(live.AttackerSide, moved.Party.MapEventSide);
            Assert.Contains(live.AttackerSide.Parties, party => ReferenceEquals(party.Party, moved.Party));
            Assert.Equal(MoveModeType.Party, moved.PartyMoveMode);
            Assert.Same(target, moved.MoveTargetParty);
            Assert.False(moved.Ai.RethinkAtNextHourlyTick);
        }, JoinDisabledMethods);
    }

    [Fact]
    public void CleanUpLoadedMapEvents_LinkedPartyWithoutEntryAttachedToArmyLeader_IsUnlinked()
    {
        var mapEventContext = CreateServerMapEvent(commit: false);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.AttackerPartyId, out var leader));
            var follower = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var army = new Army(GameObjectCreator.CreateInitializedObject<Kingdom>(), leader, Army.ArmyTypes.Raider);
            follower.Army = army;
            follower.AttachedTo = leader;
            Assert.Same(mapEvent, follower.MapEvent);

            // The link survives but the side lost the entry, so releasing the leader would release
            // the follower through the attached-party cascade and read index -1 in RemovePartyInternal.
            mapEvent.AttackerSide._battleParties.RemoveAll(entry => ReferenceEquals(entry.Party, follower.Party));
            Assert.Same(mapEvent.AttackerSide, follower.Party.MapEventSide);
            ModelOrphanedAfterLoad(mapEvent);

            RunRepair(originalsAllowed: false);

            Assert.Null(leader.Party.MapEventSide);
            Assert.Null(follower.Party.MapEventSide);
            Assert.Empty(mapEvent.AttackerSide.Parties);
            Assert.Empty(mapEvent.DefenderSide.Parties);
        }, JoinDisabledMethods);
    }

    [Fact]
    public void CleanUpLoadedMapEvents_PartyDestroyedByRelease_IsRemovedFromCampaignAndRegistry()
    {
        var mapEventContext = CreateServerMapEvent(commit: false);
        var destroyedId = mapEventContext.DefenderPartyId;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(destroyedId, out var empty));

            // An active party with no troops is destroyed by HandleMapEventEnd.
            empty.IsActive = true;
            empty.MemberRoster.Clear();
            Assert.Equal(0, empty.Party.NumberOfAllMembers);
            Assert.Contains(empty, Campaign.Current.CampaignObjectManager.MobileParties);
            ModelOrphanedAfterLoad(mapEvent);

            // Production runs the repair at CampaignReady, when destroy patches still let originals run
            // and so publish no InstanceDestroyed of their own.
            RunRepair(originalsAllowed: true);

            Assert.DoesNotContain(empty, Campaign.Current.CampaignObjectManager.MobileParties);
            Assert.False(Server.ObjectManager.TryGetObject<MobileParty>(destroyedId, out _));
        }, JoinDisabledMethods);

        foreach (var client in Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<MobileParty>(destroyedId, out _));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CleanUpLoadedMapEvents_PartyDestroyedByFinalize_IsRemovedFromCampaignAndRegistry(bool halfFinalized)
    {
        var mapEventContext = CreateServerMapEvent(commit: false);
        var destroyedId = mapEventContext.DefenderPartyId;
        RegisterAsPlayerParty(
            "offline-player-destroyed-defender",
            TestEnvironment.CreateRegisteredObject<Hero>(),
            mapEventContext.AttackerPartyId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(destroyedId, out var empty));

            Server.Resolve<IMessageBroker>().Publish(this, new SavedPlayerRegistrationsRestored());

            // An active party with no troops is destroyed by HandleMapEventEnd.
            empty.IsActive = true;
            empty.MemberRoster.Clear();
            Assert.Equal(0, empty.Party.NumberOfAllMembers);
            Assert.Contains(empty, Campaign.Current.CampaignObjectManager.MobileParties);

            // false takes the FinalizeEvent branch, true the DetachFinalizedEvent branch.
            if (halfFinalized)
                ModelOrphanedAfterLoad(mapEvent);

            // Production runs the load repair at CampaignReady, when destroy patches still let originals
            // run and so publish no InstanceDestroyed of their own.
            using (new AllowedThread())
            {
                Server.Resolve<IMapEventLoadCleaner>().CleanUpLoadedMapEvents();
            }

            Assert.DoesNotContain(empty, Campaign.Current.CampaignObjectManager.MobileParties);
            Assert.False(Server.ObjectManager.TryGetObject<MobileParty>(destroyedId, out _));
        }, JoinDisabledMethods);

        foreach (var client in Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<MobileParty>(destroyedId, out _));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CleanUpLoadedMapEvents_OneEventThrows_FinalizesTheOthers(bool registerBrokenFirst)
    {
        var brokenContext = CreateServerMapEvent(commit: false);
        var healthyContext = CreateServerMapEvent(commit: false);

        // The player manager does not promise an order, so both registration orders run and one of
        // them processes the broken event before the healthy one.
        void RegisterBroken() => RegisterAsPlayerParty(
            "offline-player-broken-event",
            TestEnvironment.CreateRegisteredObject<Hero>(),
            brokenContext.AttackerPartyId);
        void RegisterHealthy() => RegisterAsPlayerParty(
            "offline-player-healthy-event",
            TestEnvironment.CreateRegisteredObject<Hero>(),
            healthyContext.AttackerPartyId);

        if (registerBrokenFirst)
        {
            RegisterBroken();
            RegisterHealthy();
        }
        else
        {
            RegisterHealthy();
            RegisterBroken();
        }

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(brokenContext.MapEventId, out var broken));
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(healthyContext.MapEventId, out var healthy));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(healthyContext.AttackerPartyId, out var healthyAttacker));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(healthyContext.DefenderPartyId, out var healthyDefender));

            Server.Resolve<IMessageBroker>().Publish(this, new SavedPlayerRegistrationsRestored());

            // A live event the load never restored: the component lost its back-reference to the event,
            // which FieldBattleEventComponent reads when FinalizeEvent removes the parties.
            var component = Assert.IsType<FieldBattleEventComponent>(broken.Component);
            component.MapEvent = null!;
            Assert.False(broken.IsFinalized);

            Server.Resolve<IMapEventLoadCleaner>().CleanUpLoadedMapEvents();

            Assert.Equal(MapEventState.WaitingRemoval, healthy.State);
            Assert.Null(healthyAttacker.Party.MapEventSide);
            Assert.Null(healthyDefender.Party.MapEventSide);
            Assert.Empty(healthy.AttackerSide.Parties);
            Assert.Empty(healthy.DefenderSide.Parties);

            // The orphan pass runs next in production and must not leave the broken event's parties linked.
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(brokenContext.AttackerPartyId, out var brokenAttacker));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(brokenContext.DefenderPartyId, out var brokenDefender));
            Assert.Equal(MapEventState.WaitingRemoval, broken.State);
            using (new AllowedThread())
            {
                Server.Resolve<IMapEventLoadCleaner>().CleanUpLoadedMapEvents();
            }

            Assert.Null(brokenAttacker.Party.MapEventSide);
            Assert.Null(brokenDefender.Party.MapEventSide);
            Assert.Empty(broken.AttackerSide.Parties);
            Assert.Empty(broken.DefenderSide.Parties);
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void CleanUpLoadedMapEvents_FinalizeThrowsForPlayerLedArmy_StillDisbandsArmy()
    {
        var mapEventContext = CreateServerMapEvent();
        RegisterAsPlayerParty(
            "loaded-player-army-throw",
            TestEnvironment.CreateRegisteredObject<Hero>(),
            mapEventContext.AttackerPartyId);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.AttackerPartyId, out var attacker));
            var gatheringFollower = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var army = new Army(GameObjectCreator.CreateInitializedObject<Kingdom>(), attacker, Army.ArmyTypes.Raider);
            gatheringFollower.Army = army;
            Assert.Null(gatheringFollower.MapEvent);

            // A live event the load never restored: FinalizeEvent throws on the first party it removes.
            Assert.IsType<FieldBattleEventComponent>(mapEvent.Component).MapEvent = null!;
            Server.Resolve<IMessageBroker>().Publish(this, new SavedPlayerRegistrationsRestored());

            Server.Resolve<IMapEventLoadCleaner>().CleanUpLoadedMapEvents();

            Assert.Null(gatheringFollower.Army);
            Assert.Null(attacker.Army);
        }, MapEventDisabledMethods);
    }

    [Fact]
    public void CleanUpLoadedMapEvents_PartyOutsideEventDestroyedDuringPass_IsRemovedFromRegistry()
    {
        var mapEventContext = CreateServerMapEvent(commit: false);
        RegisterAsPlayerParty(
            "offline-player-outside-destroyed",
            TestEnvironment.CreateRegisteredObject<Hero>(),
            mapEventContext.AttackerPartyId);
        string? outsideId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventContext.MapEventId, out var mapEvent));
            var outside = GameObjectCreator.CreateInitializedObject<MobileParty>();
            outside.IsActive = true;
            Assert.True(Server.ObjectManager.TryGetId(outside, out outsideId));
            Assert.Contains(outside, Campaign.Current.CampaignObjectManager.MobileParties);
            Server.Resolve<IMessageBroker>().Publish(this, new SavedPlayerRegistrationsRestored());
            ModelOrphanedAfterLoad(mapEvent);

            // Stands in for a finalize cascade (a killed hero's kingdom) that destroys a party elsewhere.
            var messageBroker = Server.Resolve<IMessageBroker>();
            void DestroyOutside(MessagePayload<MapEventFinalized> payload) =>
                DestroyPartyAction.Apply(null, outside);

            messageBroker.Subscribe<MapEventFinalized>(DestroyOutside);
            try
            {
                using (new AllowedThread())
                {
                    Server.Resolve<IMapEventLoadCleaner>().CleanUpLoadedMapEvents();
                }
            }
            finally
            {
                messageBroker.Unsubscribe<MapEventFinalized>(DestroyOutside);
            }

            Assert.DoesNotContain(outside, Campaign.Current.CampaignObjectManager.MobileParties);
            Assert.False(Server.ObjectManager.TryGetObject<MobileParty>(outsideId, out _));
        }, JoinDisabledMethods);

        foreach (var client in Clients)
        {
            Assert.False(client.ObjectManager.TryGetObject<MobileParty>(outsideId, out _));
        }
    }

    private int CountFinalizedMessages(MapEvent mapEvent, Action action)
    {
        var count = 0;
        var messageBroker = Server.Resolve<IMessageBroker>();
        void Count(MessagePayload<MapEventFinalized> payload)
        {
            if (ReferenceEquals(payload.What.MapEvent, mapEvent))
                count++;
        }

        messageBroker.Subscribe<MapEventFinalized>(Count);
        try
        {
            action();
        }
        finally
        {
            messageBroker.Unsubscribe<MapEventFinalized>(Count);
        }

        return count;
    }

    private void RunRepair(bool originalsAllowed)
    {
        if (originalsAllowed)
        {
            // Production runs the repair at CampaignReady, when the sync policy still allows originals.
            using (new AllowedThread())
            {
                Server.Resolve<IMapEventLoadCleaner>().CleanUpLoadedMapEvents();
            }

            return;
        }

        Server.Resolve<IMapEventLoadCleaner>().CleanUpLoadedMapEvents();
    }

    [Fact]
    public void ReleaseEndedMapEventLinks_FirstCallbackThrows_ReleasesAndReportsRemainingParties()
    {
        // the first party's broadcast fails after the ended battle already cleared every party's side,
        // so a second run could never find the rest again
        var mapEventContext = CreateServerMapEvent(commit: false);
        var logger = new CapturingLogger();

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.AttackerPartyId, out var attacker));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(mapEventContext.DefenderPartyId, out var defender));

            var cleaner = new MapEventLoadCleaner(
                logger,
                Server.Resolve<IMessageBroker>(),
                Server.Resolve<IPlayerManager>(),
                Server.ObjectManager,
                Server.Resolve<IArmyDisbander>(),
                Server.Resolve<IStalledMapEventResolver>(),
                new OrphanedMapEventFinder(),
                Server.Resolve<ILoadRepairRegistryReconciler>());
            var reportedParties = new List<PartyBase>();

            var releasedParties = cleaner.ReleaseEndedMapEventLinks(party =>
            {
                if (reportedParties.Count == 0)
                {
                    reportedParties.Add(party);
                    throw new InvalidOperationException("broadcast failed");
                }

                reportedParties.Add(party);
            });

            Assert.Equal(2, releasedParties.Length);
            Assert.Contains(attacker.Party, releasedParties);
            Assert.Contains(defender.Party, releasedParties);
            Assert.Equal(releasedParties, reportedParties);
            Assert.Null(attacker.Party.MapEventSide);
            Assert.Null(defender.Party.MapEventSide);
        }, MapEventDisabledMethods);

        var warning = Assert.Single(logger.Events, logEvent => logEvent.Exception is InvalidOperationException);
        Assert.Equal(LogEventLevel.Warning, warning.Level);
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<LogEvent> Events { get; } = new();

        public void Write(LogEvent logEvent) => Events.Add(logEvent);
    }
}
