using Coop.Core.Server.Services.Time.Messages;
using E2E.Tests.Environment.Instance;
using GameInterface.Services.MapEvents.Messages.Start;
using E2E.Tests.Util;
using Moq;
using SandBox.GauntletUI.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

public class MapEventCollectionTests : MapEventTestBase
{
    public MapEventCollectionTests(ITestOutputHelper output) : base(output) { }

    [Fact(Skip = "MapEvent._sides is a fixed-size MapEventSide[2] array, not a dynamic collection; AssertCollectionReferenceField does not apply")]
    public void Server_MapEvent_Sides_IsFixedArray() { }

    [Fact]
    public void Server_MapEvent_Initialize_SyncsCompleteAggregateToClients()
    {
        // The helper clears messages after the two external MobileParties exist, isolating the
        // MapEvent construction/Initialize/manager-registration transaction.
        var context = CreateServerMapEvent(isolateInitializationMessages: true);

        // One aggregate is the entire initial wire protocol. Any lifetime, assignment, membership,
        // roster, visual, or AutoSync message here would recreate an observable partial state.
        var initializationTraffic = Server.NetworkSentMessages.Messages
            .Where(message => message is not CampaignTimeUpdated)
            .ToArray();
        var initialization = Assert.IsType<NetworkInitializeMapEvent>(
            Assert.Single(initializationTraffic));
        AssertSelfContainedSnapshot(context, initialization);

        // The snapshot IDs must describe the authoritative graph and the identically wired graph
        // published on every client.
        AssertCompleteGraph(Server, context, initialization);
        foreach (var client in Clients)
        {
            AssertCompleteGraph(client, context, initialization);
        }
    }

    [Fact]
    public void Server_MapEvent_Destroy_RemovesEntireAggregateGraph()
    {
        var context = CreateServerMapEvent(isolateInitializationMessages: true);
        var initialization = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkInitializeMapEvent>());
        var graphIds = EnumerateGraphIds(initialization).ToArray();

        DestroyServerMapEvent(context.MapEventId);

        AssertGraphIsUnregistered(Server, graphIds);
        foreach (var client in Clients)
            AssertGraphIsUnregistered(client, graphIds);
    }

    [Fact]
    public void Server_MapEvent_Destroy_RemovesPostCommitReinforcementGraph()
    {
        var context = CreateServerMapEvent(isolateInitializationMessages: true);
        var initialization = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkInitializeMapEvent>());
        var mapEventPartyId = JoinPartyToSide(initialization.AttackerSide.MapEventSideId);

        string[] reinforcementGraphIds = Array.Empty<string>();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEventParty>(mapEventPartyId, out var party));
            var graph = new object[]
            {
                party,
                party._woundedInBattle,
                party._diedInBattle,
                party._routedInBattle,
            };
            reinforcementGraphIds = graph.Select(instance =>
            {
                Assert.True(Server.ObjectManager.TryGetId(instance, out var id));
                return id;
            }).ToArray();
        }, MapEventDisabledMethods);

        AssertGraphIsRegistered(Server, reinforcementGraphIds);
        foreach (var client in Clients)
            AssertGraphIsRegistered(client, reinforcementGraphIds);

        DestroyServerMapEvent(context.MapEventId);

        AssertGraphIsUnregistered(Server, reinforcementGraphIds);
        foreach (var client in Clients)
            AssertGraphIsUnregistered(client, reinforcementGraphIds);
    }

    [Fact]
    public void Server_MapEvent_FinalizedBeforeFirstTick_PublishesTerminalGraphBeforeDestroy()
    {
        string? attackerId = null;
        string? defenderId = null;
        Server.Call(() =>
        {
            var attacker = GameObjectCreator.CreateInitializedObject<MobileParty>();
            var defender = GameObjectCreator.CreateInitializedObject<MobileParty>();
            Assert.True(Server.ObjectManager.TryGetId(attacker, out attackerId));
            Assert.True(Server.ObjectManager.TryGetId(defender, out defenderId));
        }, MapEventDisabledMethods);

        Server.NetworkSentMessages.Clear();
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(attackerId!, out var attacker));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(defenderId!, out var defender));

            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            mapEvent.MapEventVisual = MockMapEventVisual();
            mapEvent.Initialize(
                attacker.Party,
                defender.Party,
                new FieldBattleEventComponent(mapEvent),
                MapEvent.BattleTypes.FieldBattle);
            Campaign.Current.MapEventManager.OnMapEventCreated(mapEvent);

            // Removing the only defender finalizes the event before its first manager Tick. Vanilla has
            // already emptied that side when FinalizeEventAux runs, so this exercises the terminal aggregate.
            defender.Party.MapEventSide = null;
            Campaign.Current.MapEventManager.Tick();
        }, MapEventDisabledMethods);

        var terminal = Assert.Single(
            Server.NetworkSentMessages.GetMessages<NetworkInitializeMapEvent>());
        Assert.True(terminal.IsTerminalInitialization);
        Assert.Empty(terminal.DefenderSide.Parties);

        var graphIds = EnumerateGraphIds(terminal).ToArray();
        AssertGraphIsUnregistered(Server, graphIds);
        foreach (var client in Clients)
            AssertGraphIsUnregistered(client, graphIds);
    }

    [Fact]
    public void Client_MapEventVisualObserver_SeesOnlyFullyWiredGraph()
    {
        var client = Clients.First();
        bool callbackObserved = false;
        bool graphWasComplete = false;
        int endedCount = 0;

        client.Call(() =>
        {
            var creator = new GauntletMapEventVisualCreator();
            Campaign.Current.VisualCreator.MapEventVisualCreator = creator;
            var observer = new Mock<IGauntletMapEventVisualHandler>();
            observer
                .Setup(handler => handler.OnNewEventStarted(It.IsAny<GauntletMapEventVisual>()))
                .Callback<GauntletMapEventVisual>(visual =>
                {
                    callbackObserved = true;
                    var mapEvent = visual.MapEvent;
                    graphWasComplete = mapEvent != null
                        && ReferenceEquals(mapEvent.MapEventVisual, visual)
                        && mapEvent.Component != null
                        && mapEvent.AttackerSide?.Parties.Count > 0
                        && mapEvent.DefenderSide?.Parties.Count > 0
                        && mapEvent.AttackerSide.Parties.All(party =>
                            party.Party != null &&
                            ReferenceEquals(party.Party.MapEventSide, mapEvent.AttackerSide))
                        && mapEvent.DefenderSide.Parties.All(party =>
                            party.Party != null &&
                            ReferenceEquals(party.Party.MapEventSide, mapEvent.DefenderSide));
                });
            observer
                .Setup(handler => handler.OnEventEnded(It.IsAny<GauntletMapEventVisual>()))
                .Callback(() => endedCount++);
            creator.Handlers.Add(observer.Object);
        });

        var context = CreateServerMapEvent();

        Assert.True(callbackObserved);
        Assert.True(graphWasComplete);

        DestroyServerMapEvent(context.MapEventId);
        Assert.Equal(1, endedCount);
    }

    private static IEnumerable<string> EnumerateGraphIds(NetworkInitializeMapEvent initialization)
    {
        yield return initialization.MapEventId;
        yield return initialization.TroopUpgradeTrackerId;
        if (initialization.Component != null)
            yield return initialization.Component.ComponentId;
        if (initialization.GauntletMapEventVisualId != null)
            yield return initialization.GauntletMapEventVisualId;

        foreach (var side in new[] { initialization.DefenderSide, initialization.AttackerSide })
        {
            yield return side.MapEventSideId;
            foreach (var party in side.Parties)
            {
                yield return party.MapEventPartyId;
                yield return party.WoundedInBattleRosterId;
                yield return party.DiedInBattleRosterId;
                yield return party.RoutedInBattleRosterId;
            }
        }
    }

    private static void AssertGraphIsUnregistered(EnvironmentInstance instance, IEnumerable<string> graphIds)
    {
        instance.Call(() =>
        {
            foreach (var id in graphIds)
                Assert.False(instance.ObjectManager.Contains(id), $"Aggregate object {id} remained registered");
        });
    }

    private static void AssertGraphIsRegistered(EnvironmentInstance instance, IEnumerable<string> graphIds)
    {
        instance.Call(() =>
        {
            foreach (var id in graphIds)
                Assert.True(instance.ObjectManager.Contains(id), $"Aggregate object {id} was never registered");
        });
    }

    private static void AssertSelfContainedSnapshot(
        MapEventContext context,
        NetworkInitializeMapEvent initialization)
    {
        Assert.Equal(context.MapEventId, initialization.MapEventId);
        Assert.False(string.IsNullOrEmpty(initialization.TroopUpgradeTrackerId));
        var component = Assert.IsType<MapEventComponentInitializationData>(initialization.Component);
        Assert.Equal(context.MapEventId, component.MapEventId);
        Assert.False(string.IsNullOrEmpty(component.ComponentId));

        Assert.Equal(context.MapEventId, initialization.AttackerSide.MapEventId);
        Assert.Equal(context.MapEventId, initialization.DefenderSide.MapEventId);
        Assert.False(string.IsNullOrEmpty(initialization.AttackerSide.MapEventSideId));
        Assert.False(string.IsNullOrEmpty(initialization.DefenderSide.MapEventSideId));

        var attacker = Assert.Single(initialization.AttackerSide.Parties);
        var defender = Assert.Single(initialization.DefenderSide.Parties);
        AssertPartySnapshot(initialization.AttackerSide.MapEventSideId, attacker);
        AssertPartySnapshot(initialization.DefenderSide.MapEventSideId, defender);
    }

    private static void AssertPartySnapshot(
        string sideId,
        MapEventPartyInitializationData party)
    {
        Assert.Equal(sideId, party.MapEventSideId);
        Assert.False(string.IsNullOrEmpty(party.MapEventPartyId));
        Assert.False(string.IsNullOrEmpty(party.PartyBaseId));
        Assert.False(string.IsNullOrEmpty(party.WoundedInBattleRosterId));
        Assert.False(string.IsNullOrEmpty(party.DiedInBattleRosterId));
        Assert.False(string.IsNullOrEmpty(party.RoutedInBattleRosterId));
        Assert.NotEmpty(party.FlattenedTroops);
        Assert.All(party.FlattenedTroops, troop => Assert.False(string.IsNullOrEmpty(troop.ObjectId)));
    }

    private static void AssertCompleteGraph(
        EnvironmentInstance instance,
        MapEventContext context,
        NetworkInitializeMapEvent initialization)
    {
        instance.Call(() =>
        {
            Assert.True(instance.ObjectManager.TryGetObject<MapEvent>(initialization.MapEventId, out var mapEvent));
            Assert.Contains(mapEvent, Campaign.Current.MapEventManager.MapEvents);

            Assert.True(instance.ObjectManager.TryGetObject<MapEventSide>(
                initialization.AttackerSide.MapEventSideId,
                out var attackerSide));
            Assert.True(instance.ObjectManager.TryGetObject<MapEventSide>(
                initialization.DefenderSide.MapEventSideId,
                out var defenderSide));
            Assert.Same(attackerSide, mapEvent.AttackerSide);
            Assert.Same(defenderSide, mapEvent.DefenderSide);
            Assert.Same(mapEvent, attackerSide.MapEvent);
            Assert.Same(mapEvent, defenderSide.MapEvent);

            Assert.True(instance.ObjectManager.TryGetObject<TroopUpgradeTracker>(
                initialization.TroopUpgradeTrackerId,
                out var tracker));
            Assert.Same(tracker, mapEvent.TroopUpgradeTracker);

            if (initialization.Component == null)
            {
                Assert.Null(mapEvent.Component);
            }
            else
            {
                Assert.True(instance.ObjectManager.TryGetObject<MapEventComponent>(
                    initialization.Component.ComponentId,
                    out var component));
                Assert.Same(component, mapEvent.Component);
                Assert.Same(mapEvent, component.MapEvent);
            }

            AssertPartyGraph(
                instance,
                attackerSide,
                context.AttackerPartyId,
                Assert.Single(initialization.AttackerSide.Parties));
            AssertPartyGraph(
                instance,
                defenderSide,
                context.DefenderPartyId,
                Assert.Single(initialization.DefenderSide.Parties));
        });
    }

    private static void AssertPartyGraph(
        EnvironmentInstance instance,
        MapEventSide side,
        string mobilePartyId,
        MapEventPartyInitializationData expected)
    {
        Assert.True(instance.ObjectManager.TryGetObject<MapEventParty>(expected.MapEventPartyId, out var mapEventParty));
        Assert.Same(mapEventParty, Assert.Single(side.Parties));

        Assert.True(instance.ObjectManager.TryGetObject<PartyBase>(expected.PartyBaseId, out var partyBase));
        Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(mobilePartyId, out var mobileParty));
        Assert.Same(partyBase, mapEventParty.Party);
        Assert.Same(mobileParty.Party, mapEventParty.Party);
        Assert.Same(side, partyBase.MapEventSide);
        Assert.Same(side.MapEvent, mobileParty.MapEvent);
        Assert.Equal(expected.MobilePartyPosition, mobileParty.Position);
        Assert.Equal(expected.EventPositionAdderX, mobileParty.EventPositionAdder.X);
        Assert.Equal(expected.EventPositionAdderY, mobileParty.EventPositionAdder.Y);

        AssertRegisteredRoster(instance, expected.WoundedInBattleRosterId, mapEventParty._woundedInBattle);
        AssertRegisteredRoster(instance, expected.DiedInBattleRosterId, mapEventParty._diedInBattle);
        AssertRegisteredRoster(instance, expected.RoutedInBattleRosterId, mapEventParty._routedInBattle);
        Assert.Equal(expected.FlattenedTroops.Length, mapEventParty._roster.Count());
    }

    private static void AssertRegisteredRoster(
        EnvironmentInstance instance,
        string rosterId,
        TroopRoster expected)
    {
        Assert.True(instance.ObjectManager.TryGetObject<TroopRoster>(rosterId, out var registered));
        Assert.Same(expected, registered);
    }
}
