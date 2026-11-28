using Common.Messaging;
using Common.Network.Messages;
using Coop.Core.Client.Services.MobileParties.Messages;
using E2E.Tests.Environment.Instance;
using E2E.Tests.Services.MapEvents;
using E2E.Tests.Services.Settlements;
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

        StartLeaderBattle(leaderId, partyId, armyId);

        // Parking waits for the battle to end
        Disconnect();
        AssertActive(partyId, expected: true);

        EndLeaderBattle(leaderId);

        AssertActive(partyId, expected: false);
        AssertNotFollowing(partyId, armyId);
    }

    // #4857: vanilla skips a party attached to another party's army, so the town is checked only once the follower left it
    [Fact]
    public void Disconnect_FollowerInHostileTown_IsEjectedAfterLeavingArmy()
    {
        var leaderId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var (partyId, armyId) = SetupFollower(leaderId);
        var settlementId = PutInTownAtWarWithFollower(partyId, leaderId);

        Disconnect();

        AssertNotFollowing(partyId, armyId);
        AssertEjected(partyId, settlementId);
    }

    // After a battle the army leave is queued past the park, so the town is checked again once the follower left the army
    [Fact]
    public void DisconnectDuringLeaderBattle_FollowerInHostileTown_IsEjectedAfterQueuedArmyLeave()
    {
        var leaderId = TestEnvironment.CreateRegisteredObject<MobileParty>();
        var (partyId, armyId) = SetupFollower(leaderId);
        var memberId = StartLeaderBattle(leaderId, partyId, armyId);
        var settlementId = PutInTownAtWarWithFollower(partyId, leaderId, memberId);

        Disconnect();
        AssertActive(partyId, expected: true);
        EndLeaderBattle(leaderId);

        AssertNotFollowing(partyId, armyId);
        AssertEjected(partyId, settlementId);
    }

    // An AI member attached after the follower is the side's last party, so the leader's battle finalizes only once the follower is out of it
    private string StartLeaderBattle(string leaderId, string partyId, string armyId)
    {
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
        return memberId;
    }

    // The leader leaving takes its attached parties with it, and the battle finalizes inside vanilla's loop over them
    private void EndLeaderBattle(string leaderId)
    {
        Server.Call(() =>
        {
            var leader = Server.GetRegisteredObject<MobileParty>(leaderId);
            leader.Party.MapEventSide = null;
        }, MapEventDisabledMethods);
        PumpAll();
    }

    // The parties go in a town whose clan then goes to war with the follower's clan, which keeps the online follower inside
    private string PutInTownAtWarWithFollower(string partyId, params string[] otherPartyIds)
    {
        string? clanId = null;
        Server.Call(() => Assert.True(Server.ObjectManager.TryGetId(Server.GetRegisteredObject<MobileParty>(partyId).ActualClan, out clanId)));
        var (settlementId, townClanId) = HostileTownSetup.CreateTown(TestEnvironment, clanId!, otherPartyIds.Prepend(partyId).ToArray());
        HostileTownSetup.DeclareWar(TestEnvironment, townClanId, clanId!);

        Assert.Empty(LeavesOf(partyId));
        HostileTownSetup.AssertInTown(TestEnvironment, partyId, settlementId, inTown: true);
        return settlementId;
    }

    private void AssertEjected(string partyId, string settlementId)
    {
        Assert.Single(LeavesOf(partyId));
        HostileTownSetup.AssertInTown(TestEnvironment, partyId, settlementId, inTown: false);
    }

    private IEnumerable<NetworkPartyLeaveSettlement> LeavesOf(string partyId)
    {
        var handle = Server.GetHandle<MobileParty>(partyId);
        return Server.NetworkSentMessages.GetMessages<NetworkPartyLeaveSettlement>().Where(message => message.PartyId == handle);
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
        Server.PumpGameThread();
        foreach (var client in Clients)
            client.PumpGameThread();
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
