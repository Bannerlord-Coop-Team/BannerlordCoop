using Coop.Core.Server.Services.Time.Messages;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Util;
using GameInterface.Services.MapEvents.Initialization;
using GameInterface.Services.MapEvents.Messages.Start;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using Xunit.Abstractions;

namespace E2E.Tests.Services.MapEvents;

public class MapEventCollectionTests : MapEventTestBase
{
    public MapEventCollectionTests(ITestOutputHelper output) : base(output) { }

    [Fact(Skip = "MapEvent._sides is a fixed-size MapEventSide[2] array")]
    public void Server_MapEvent_Sides_IsFixedArray() { }

    [Fact]
    public void Server_MapEvent_Barrier_PublishesOnlyCompleteGraph()
    {
        var staged = StageMapEvent();

        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkMapEventInitialized>());
        ForClients(client => AssertNotPublished(client, staged.MapEventId));

        Commit(staged.MapEventId);

        var barrier = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkMapEventInitialized>());
        Assert.Equal(staged.MapEventId, barrier.MapEventId);
        Assert.False(barrier.IsTerminal);
        var last = Server.NetworkSentMessages.Messages
            .Where(message => message is not CampaignTimeUpdated)
            .Last();
        Assert.Equal(barrier, Assert.IsType<NetworkMapEventInitialized>(last));

        ForAll(instance => AssertCompleteGraph(instance, staged));
    }

    [Fact]
    public void Server_MapEvent_FinalizedBeforeCommit_IsNeverPublished()
    {
        var staged = StageMapEvent();
        var ownedIds = CaptureOwnedIds(staged.MapEventId);

        Server.Call(() =>
        {
            var mapEvent = Get<MapEvent>(Server, staged.MapEventId);
            Server.Resolve<IMapEventInitializationBarrier>().CommitTerminalServer(mapEvent);
            mapEvent.FinalizeEvent();
        }, MapEventDisabledMethods);

        var terminal = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkMapEventInitialized>());
        Assert.Equal(staged.MapEventId, terminal.MapEventId);
        Assert.True(terminal.IsTerminal);
        ForAll(instance =>
        {
            AssertNotPublished(instance, staged.MapEventId);
            AssertRegistration(instance, ownedIds, expected: false);
        });
        AssertExternalPartiesRemain(staged.AttackerPartyId, staged.DefenderPartyId);
    }

    [Fact]
    public void Server_MapEvent_Destroy_RemovesInitialAndReinforcementGraph()
    {
        var staged = StageMapEvent();
        Commit(staged.MapEventId);
        Server.Call(() =>
        {
            var mapEvent = Get<MapEvent>(Server, staged.MapEventId);
            mapEvent.TroopUpgradeTracker = null!;
            Assert.NotNull(mapEvent.TroopUpgradeTracker);
        }, MapEventDisabledMethods);
        var initialIds = CaptureOwnedIds(staged.MapEventId);
        var reinforcementId = JoinPartyToSide(staged.AttackerSide.Id);

        string? reinforcementPartyId = null;
        string? currentTrackerId = null;
        Server.Call(() =>
        {
            var reinforcement = Get<MapEventParty>(Server, reinforcementId);
            Assert.True(Server.ObjectManager.TryGetId(reinforcement.Party.MobileParty, out reinforcementPartyId));
            var mapEvent = Get<MapEvent>(Server, staged.MapEventId);
            Assert.True(Server.ObjectManager.TryGetId(mapEvent.TroopUpgradeTracker, out currentTrackerId));
        }, MapEventDisabledMethods);
        Assert.NotNull(reinforcementPartyId);
        Assert.NotNull(currentTrackerId);

        var ownedIds = initialIds.Concat(CaptureOwnedIds(staged.MapEventId)).Distinct().ToArray();
        ForAll(instance => AssertRegistration(instance, ownedIds, expected: true));
        ForAll(instance => AssertCompleteGraph(instance, staged, 2, currentTrackerId));

        DestroyServerMapEvent(staged.MapEventId);

        ForAll(instance => AssertRegistration(instance, ownedIds, expected: false));
        AssertExternalPartiesRemain(
            staged.AttackerPartyId,
            staged.DefenderPartyId,
            reinforcementPartyId!);
    }

    private StagedMapEvent StageMapEvent()
    {
        var attackerId = TestEnvironment.CreateRegisteredObject<MobileParty>(MapEventDisabledMethods);
        var defenderId = TestEnvironment.CreateRegisteredObject<MobileParty>(MapEventDisabledMethods);
        Server.NetworkSentMessages.Clear();

        StagedMapEvent? staged = null;
        Server.Call(() =>
        {
            Server.NetworkSentMessages.Clear();
            var attacker = Get<MobileParty>(Server, attackerId);
            var defender = Get<MobileParty>(Server, defenderId);
            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            mapEvent.MapEventVisual = MockMapEventVisual();
            mapEvent.Initialize(
                attacker.Party,
                defender.Party,
                new FieldBattleEventComponent(mapEvent),
                MapEvent.BattleTypes.FieldBattle);

            var retiredTrackerId = Id(mapEvent.TroopUpgradeTracker);
            mapEvent.TroopUpgradeTracker = null!;
            mapEvent.TroopUpgradeTracker = new TroopUpgradeTracker();

            staged = new StagedMapEvent(
                Id(mapEvent),
                attackerId,
                defenderId,
                Id(mapEvent.Component),
                Id(mapEvent.TroopUpgradeTracker),
                retiredTrackerId,
                Side(mapEvent.AttackerSide),
                Side(mapEvent.DefenderSide));
            Assert.DoesNotContain(mapEvent, Campaign.Current.MapEventManager.MapEvents);

            SideIds Side(MapEventSide side) =>
                new(Id(side), Party(Assert.Single(side.Parties)));

            PartyIds Party(MapEventParty party) => new(
                Id(party),
                Id(party._woundedInBattle),
                Id(party._diedInBattle),
                Id(party._routedInBattle));

            string Id(object instance)
            {
                Assert.True(Server.ObjectManager.TryGetId(instance, out var id));
                return id;
            }
        }, MapEventDisabledMethods);

        Assert.NotNull(staged);
        ForAll(instance => AssertRegistration(instance, new[] { staged!.RetiredTrackerId }, expected: false));
        return staged!;
    }

    private void Commit(string mapEventId)
    {
        Server.Call(() =>
        {
            var mapEvent = Get<MapEvent>(Server, mapEventId);
            var manager = Campaign.Current.MapEventManager;
            if (!manager.MapEvents.Contains(mapEvent)) manager.OnMapEventCreated(mapEvent);
            Server.Resolve<IMapEventInitializationBarrier>().CommitServer(mapEvent);
        }, MapEventDisabledMethods);
    }

    private string[] CaptureOwnedIds(string mapEventId)
    {
        var ids = new List<string>();
        Server.Call(() =>
        {
            var mapEvent = Get<MapEvent>(Server, mapEventId);
            Add(mapEvent);
            Add(mapEvent.Component);
            Add(mapEvent.TroopUpgradeTracker);
            foreach (var side in mapEvent._sides.Where(side => side != null))
            {
                Add(side);
                foreach (var party in side.Parties)
                {
                    Add(party);
                    Add(party._woundedInBattle);
                    Add(party._diedInBattle);
                    Add(party._routedInBattle);
                }
            }

            void Add(object instance)
            {
                Assert.True(Server.ObjectManager.TryGetId(instance, out var id));
                ids.Add(id);
            }
        }, MapEventDisabledMethods);
        return ids.Distinct().ToArray();
    }

    private static void AssertCompleteGraph(
        EnvironmentInstance instance,
        StagedMapEvent staged,
        int attackerCount = 1,
        string? trackerId = null)
    {
        instance.Call(() =>
        {
            var mapEvent = Get<MapEvent>(instance, staged.MapEventId);
            Assert.Single(Campaign.Current.MapEventManager.MapEvents, item => ReferenceEquals(item, mapEvent));

            var component = Get<MapEventComponent>(instance, staged.ComponentId);
            Assert.Same(component, mapEvent.Component);
            Assert.Same(mapEvent, component.MapEvent);
            Assert.Same(Get<TroopUpgradeTracker>(instance, trackerId ?? staged.TrackerId), mapEvent.TroopUpgradeTracker);

            var attacker = Get<MapEventSide>(instance, staged.AttackerSide.Id);
            var defender = Get<MapEventSide>(instance, staged.DefenderSide.Id);
            Assert.Same(attacker, mapEvent.AttackerSide);
            Assert.Same(defender, mapEvent.DefenderSide);
            Assert.Same(mapEvent, attacker.MapEvent);
            Assert.Same(mapEvent, defender.MapEvent);
            Assert.Equal(attackerCount, attacker.Parties.Count);
            Assert.Single(defender.Parties);
            AssertParty(staged.AttackerPartyId, attacker, attacker.Parties[0], staged.AttackerSide.InitialParty);
            AssertParty(staged.DefenderPartyId, defender, defender.Parties[0], staged.DefenderSide.InitialParty);

            foreach (var party in attacker.Parties.Concat(defender.Parties))
            {
                Assert.True(instance.ObjectManager.Contains(party));
                Assert.True(instance.ObjectManager.Contains(party._woundedInBattle));
                Assert.True(instance.ObjectManager.Contains(party._diedInBattle));
                Assert.True(instance.ObjectManager.Contains(party._routedInBattle));
                Assert.NotNull(party._roster);
            }

            void AssertParty(string mobileId, MapEventSide side, MapEventParty party, PartyIds expected)
            {
                Assert.Same(Get<MapEventParty>(instance, expected.Id), party);
                var mobile = Get<MobileParty>(instance, mobileId);
                Assert.Same(mobile.Party, party.Party);
                Assert.Same(side, party.Party.MapEventSide);
                Assert.Same(mapEvent, mobile.MapEvent);
                Assert.Same(Get<TroopRoster>(instance, expected.WoundedId), party._woundedInBattle);
                Assert.Same(Get<TroopRoster>(instance, expected.DiedId), party._diedInBattle);
                Assert.Same(Get<TroopRoster>(instance, expected.RoutedId), party._routedInBattle);
            }
        });
    }

    private static T Get<T>(EnvironmentInstance instance, string id) where T : class
    {
        Assert.True(instance.ObjectManager.TryGetObject<T>(id, out var value));
        return value;
    }

    private static void AssertNotPublished(EnvironmentInstance instance, string id)
    {
        instance.Call(() =>
        {
            if (instance.ObjectManager.TryGetObject<MapEvent>(id, out var mapEvent))
                Assert.DoesNotContain(mapEvent, Campaign.Current.MapEventManager.MapEvents);
            Assert.DoesNotContain(
                Campaign.Current.MapEventManager.MapEvents,
                item => item.StringId != null && (item.StringId == id || id.EndsWith("_" + item.StringId)));
        });
    }

    private static void AssertRegistration(EnvironmentInstance instance, IEnumerable<string> ids, bool expected)
    {
        instance.Call(() =>
        {
            foreach (var id in ids) Assert.Equal(expected, instance.ObjectManager.Contains(id));
        });
    }

    private void AssertExternalPartiesRemain(params string[] ids) => ForAll(instance => instance.Call(() =>
    {
        foreach (var id in ids) Assert.Null(Get<MobileParty>(instance, id).Party.MapEventSide);
    }));

    private void ForClients(Action<EnvironmentInstance> action)
    {
        foreach (var client in Clients) action(client);
    }

    private void ForAll(Action<EnvironmentInstance> action)
    {
        action(Server);
        ForClients(action);
    }

    private sealed record StagedMapEvent(
        string MapEventId,
        string AttackerPartyId,
        string DefenderPartyId,
        string ComponentId,
        string TrackerId,
        string RetiredTrackerId,
        SideIds AttackerSide,
        SideIds DefenderSide);

    private sealed record SideIds(string Id, PartyIds InitialParty);
    private sealed record PartyIds(string Id, string WoundedId, string DiedId, string RoutedId);
}
