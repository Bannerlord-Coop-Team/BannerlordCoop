using Common.Messaging;
using Common.Network.Messages;
using Coop.Core.Server.Connections.Messages;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using E2E.Tests.Util;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Armies;

/// <summary>
/// A player following another lord's army disconnects and is parked (#4941).
/// </summary>
public class ParkedArmyFollowerTests : MapEventTestBase
{
    private const string ControllerId = "follower";

    private EnvironmentInstance Client => Clients.First();

    public ParkedArmyFollowerTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void Disconnect_FollowerLeavesArmy_AndIsNotPulledIntoLeaderBattle()
    {
        var leaderId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var (partyId, armyId) = SetupFollower(leaderId);

        Disconnect();
        StartLeaderBattle(leaderId);
        AssertNotInBattle(partyId);

        Reconnect();
        AssertNotFollowing(partyId, armyId);
        AssertNotInBattle(partyId);
        AssertActive(partyId, expected: true);
    }

    [Fact]
    public void DisconnectDuringLeaderBattle_FollowerLeavesArmyWhenTheBattleEnds()
    {
        var leaderId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var (partyId, armyId) = SetupFollower(leaderId);
        var mapEventId = StartLeaderBattle(leaderId);
        Server.Call(() => Assert.Same(Server.GetRegisteredObject<MapEvent>(mapEventId), Server.GetRegisteredObject<MobileParty>(partyId).MapEvent));

        // Parking waits for the battle to end
        Disconnect();
        AssertActive(partyId, expected: true);

        DestroyServerMapEvent(mapEventId);
        PumpAll();

        AssertActive(partyId, expected: false);
        AssertNotFollowing(partyId, armyId);
    }

    // A connected player's party following the AI leader's army, attached to the leader.
    private (string PartyId, string ArmyId) SetupFollower(string leaderId)
    {
        var (_, partyId) = CreatePlayerHeroParty(ControllerId);
        TestEnvironment.ConnectRegisteredPlayer(Client, ControllerId);

        string? armyId = null;
        Server.Call(() =>
        {
            var leader = Server.GetRegisteredObject<MobileParty>(leaderId);
            var army = new Army(GameObjectCreator.CreateInitializedObject<Kingdom>(), leader, Army.ArmyTypes.Patrolling);
            Assert.True(Server.ObjectManager.TryGetId(army, out armyId));
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            party.Army = army;
            party.AttachedTo = leader;
        }, MapEventDisabledMethods);
        PumpAll();

        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                var party = instance.GetRegisteredObject<MobileParty>(partyId);
                Assert.Same(instance.GetRegisteredObject<Army>(armyId!), party.Army);
                Assert.Same(instance.GetRegisteredObject<MobileParty>(leaderId), party.AttachedTo);
            });
        }

        return (partyId, armyId!);
    }

    // The leader starts a field battle, which vanilla copies onto the parties attached to it.
    private string StartLeaderBattle(string leaderId)
    {
        string? mapEventId = null;
        Server.Call(() =>
        {
            var leader = Server.GetRegisteredObject<MobileParty>(leaderId);
            var mapEvent = GameObjectCreator.CreateInitializedObject<MapEvent>();
            var defender = GameObjectCreator.CreateInitializedObject<MobileParty>();
            mapEvent.MapEventVisual = MockMapEventVisual();
            mapEvent.Initialize(leader.Party, defender.Party, new FieldBattleEventComponent(mapEvent));
            mapEvent.MapEventVisual = null;
            if (!Campaign.Current.MapEventManager.MapEvents.Contains(mapEvent))
                Campaign.Current.MapEventManager.OnMapEventCreated(mapEvent);
            Assert.True(Server.ObjectManager.TryGetId(mapEvent, out mapEventId));
        }, MapEventDisabledMethods);
        PumpAll();
        return mapEventId!;
    }

    private void Disconnect()
    {
        Server.Call(() => Server.Resolve<IMessageBroker>().Publish(this, new PlayerDisconnected(Client.NetPeer, default)));
        PumpAll();
    }

    private void Reconnect()
    {
        TestEnvironment.ConnectRegisteredPlayer(Client, ControllerId);
        Server.Call(() => Server.Resolve<IMessageBroker>().Publish(this, new PlayerCampaignSynchronized(Client.NetPeer)));
        PumpAll();
    }

    private void PumpAll()
    {
        for (var i = 0; i < 3; i++)
        {
            Server.PumpGameThread();
            foreach (var client in Clients)
                client.PumpGameThread();
        }
    }

    private void AssertNotFollowing(string partyId, string armyId)
    {
        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                var name = instance == Server ? "server" : "client";
                var party = instance.GetRegisteredObject<MobileParty>(partyId);
                Assert.True(party.Army == null, $"{name}: party still in army {party.Army?.Name}");
                Assert.True(party.AttachedTo == null, $"{name}: party still attached to {party.AttachedTo?.StringId}");
                if (instance.ObjectManager.TryGetObject<Army>(armyId, out var army))
                    Assert.DoesNotContain(party, army.Parties);
            });
        }
    }

    private void AssertNotInBattle(string partyId)
    {
        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                var party = instance.GetRegisteredObject<MobileParty>(partyId);
                Assert.True(party.Party.MapEventSide == null,
                    $"{(instance == Server ? "server" : "client")}: party in battle {party.MapEvent?.StringId}");
            });
        }
    }

    private void AssertActive(string partyId, bool expected)
    {
        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                var party = instance.GetRegisteredObject<MobileParty>(partyId);
                Assert.True(party.IsActive == expected,
                    $"{(instance == Server ? "server" : "client")}: party active is {party.IsActive}");
            });
        }
    }
}
