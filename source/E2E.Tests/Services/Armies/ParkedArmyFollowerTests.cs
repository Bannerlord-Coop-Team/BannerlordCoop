using Common.Messaging;
using Common.Network.Messages;
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
        AssertNotFollowing(partyId, armyId);

        CreateServerMapEvent(attackerId: leaderId);
        PumpAll();
        AssertNotInBattle(partyId);
    }

    [Fact]
    public void DisconnectDuringLeaderBattle_FollowerLeavesArmyWhenTheLeaderLeavesTheBattle()
    {
        var leaderId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var (partyId, armyId) = SetupFollower(leaderId);

        // The AI member is attached after the follower so it is the side's last party, which makes the battle finalize
        // only once the follower is already out of it
        var memberId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        Server.Call(() =>
        {
            var member = Server.GetRegisteredObject<MobileParty>(memberId);
            member.Army = Server.GetRegisteredObject<Army>(armyId);
            member.AttachedTo = Server.GetRegisteredObject<MobileParty>(leaderId);
        }, MapEventDisabledMethods);
        PumpAll();

        var mapEventId = CreateServerMapEvent(attackerId: leaderId).MapEventId;
        PumpAll();
        Server.Call(() =>
        {
            var mapEvent = Server.GetRegisteredObject<MapEvent>(mapEventId);
            var party = Server.GetRegisteredObject<MobileParty>(partyId);
            Assert.Same(mapEvent, party.MapEvent);
        });

        // Parking waits for the battle to end
        Disconnect();
        AssertActive(partyId, expected: true);

        // The leader leaving takes its attached parties with it, and the battle finalizes inside vanilla's loop over them
        Server.Call(() =>
        {
            var leader = Server.GetRegisteredObject<MobileParty>(leaderId);
            leader.Party.MapEventSide = null;
        }, MapEventDisabledMethods);
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

    private void Disconnect()
    {
        Server.Call(() => Server.Resolve<IMessageBroker>().Publish(this, new PlayerDisconnected(Client.NetPeer, default)));
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
                var army = instance.GetRegisteredObject<Army>(armyId);
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
                var name = instance == Server ? "server" : "client";
                var party = instance.GetRegisteredObject<MobileParty>(partyId);
                Assert.True(party.Party.MapEventSide == null, $"{name}: party in battle {party.MapEvent?.StringId}");
            });
        }
    }

    private void AssertActive(string partyId, bool expected)
    {
        foreach (var instance in Clients.Append(Server))
        {
            instance.Call(() =>
            {
                var name = instance == Server ? "server" : "client";
                var party = instance.GetRegisteredObject<MobileParty>(partyId);
                Assert.True(party.IsActive == expected, $"{name}: party active is {party.IsActive}");
            });
        }
    }
}
