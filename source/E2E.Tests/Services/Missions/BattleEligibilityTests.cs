using Common.Network;
using Common.Util;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MapEvents.TroopSupply.Messages;
using GameInterface.Services.MapEventSides.Messages;
using GameInterface.Services.Players;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// End-to-end tests for BR-004 Player Eligibility: only a player whose party is a valid participant in the
/// corresponding map event may join (host) the battle mission. The server's
/// <c>BattleHostHandler.IsRequesterInBattle</c> guard rejects a host-election request from a controller whose
/// party is not in the map event, so no host/successor entry is ever created for that outsider. Uses three
/// players (two valid participants + one outsider) and the campaign <c>INetwork</c> round-trip the E2E mock
/// router replicates.
/// </summary>
public class BattleEligibilityTests : MissionTestEnvironment
{
    public BattleEligibilityTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    [Fact]
    [Trait("Requirement", "BR-004")]
    public void NonParticipantHostRequest_IsRejected_NoHostEntryCreated()
    {
        // ctrl-A and ctrl-B are valid map-event participants; the third client is an OUTSIDER whose party is
        // never added to this map event.
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B");
        var clients = Clients.ToArray();

        EnterBattle(clients[0], mapEventId); // ctrl-A, a valid participant, is the sole host so far
        AssertHost(Server, mapEventId, "ctrl-A");

        // Register an outsider with the optimistic back-reference that used to be mistaken for membership.
        var outsiderPartyId = CreateRegisteredObject<MobileParty>(MapEventDisabledMethods);
        var outsiderHeroId = CreateRegisteredObject<Hero>();
        SetControllerId(clients[2], "ctrl-Outsider");
        RegisterAsPlayerParty("ctrl-Outsider", outsiderHeroId, outsiderPartyId);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(outsiderPartyId, out var outsiderParty));
            outsiderParty.Party._mapEventSide = mapEvent.AttackerSide;

            Assert.Same(mapEvent, outsiderParty.MapEvent);
            Assert.DoesNotContain(mapEvent.AttackerSide.Parties, party => party.Party == outsiderParty.Party);
        });

        // The outsider tries to join/host the battle. BR-004: it has no MapEventParty, so the server's
        // eligibility guard rejects the election request. The outsider must not become host or be appended to
        // the successor line, on any instance.
        EnterBattle(clients[2], mapEventId);

        AssertHost(Server, mapEventId, "ctrl-A");
        foreach (var client in Clients)
            AssertHost(client, mapEventId, "ctrl-A");
        AssertIsLocalHost(clients[2], mapEventId, false); // the outsider never became host
    }

    [Fact]
    [Trait("Requirement", "BR-004")]
    public void MissionStart_RejectsNonMember_AndTargetsAuthoritativeParticipants()
    {
        var (mapEventId, partyIds) = SetupCoopBattle("ctrl-A", "ctrl-B");
        var clients = Clients.ToArray();
        var outsiderPartyId = CreateRegisteredObject<MobileParty>(MapEventDisabledMethods);
        var outsiderHeroId = CreateRegisteredObject<Hero>();
        var troopId = CreateRegisteredObject<CharacterObject>();
        SetControllerId(clients[2], "ctrl-Outsider");
        RegisterAsPlayerParty("ctrl-Outsider", outsiderHeroId, outsiderPartyId);

        Server.Resolve<IPlayerManager>().SetPeer("ctrl-A", clients[0].NetPeer);
        Server.Resolve<IPlayerManager>().SetPeer("ctrl-B", clients[1].NetPeer);
        Server.Resolve<IPlayerManager>().SetPeer("ctrl-Outsider", clients[2].NetPeer);

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyIds[0], out var attacker));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyIds[1], out var defender));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(outsiderPartyId, out var outsider));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            outsider.Party._mapEventSide = mapEvent.AttackerSide;

            using (new AllowedThread())
            {
                attacker.MemberRoster.AddToCounts(troop, 5);
                defender.MemberRoster.AddToCounts(troop, 5);
            }

            Assert.DoesNotContain(mapEvent.AttackerSide.Parties, party => party.Party == outsider.Party);
        }, MapEventDisabledMethods);

        foreach (var client in clients)
            client.InternalMessages.Clear();

        try
        {
            Server.NetworkSentMessages.Clear();
            clients[2].Call(() => clients[2].Resolve<INetwork>().SendAll(new NetworkBattleStartRequest(
                Guid.NewGuid().ToString(),
                (int)BattleStartMode.Mission,
                mapEventId,
                outsiderPartyId)), MapEventDisabledMethods);

            Assert.False(Server.NetworkSentMessages.GetMessages<NetworkBattleStartReply>().Single().Accepted);
            Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkStartAttackMission>());
            Assert.Equal(
                new[] { 0, 0, 0 },
                clients.Select(client => client.InternalMessages.GetMessageCount<NetworkStartAttackMission>()));
            Server.Call(() => Assert.False(ServerBattleModeArbiter.TryGetMode(mapEventId, out _)));

            Server.NetworkSentMessages.Clear();
            Server.InternalMessages.Clear();

            clients[1].Call(() =>
            {
                Assert.True(clients[1].ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
                Assert.True(clients[1].ObjectManager.TryGetObject<MobileParty>(partyIds[1], out var participant));
                var mapEventParty = Assert.Single(
                    mapEvent.DefenderSide.Parties,
                    party => party.Party == participant.Party);
                mapEvent.DefenderSide._battleParties.Remove(mapEventParty);
                participant.Party._mapEventSide = mapEvent.DefenderSide;
                Assert.DoesNotContain(
                    mapEvent.DefenderSide.Parties,
                    party => party.Party == participant.Party);
            });

            clients[0].Call(() => clients[0].Resolve<INetwork>().SendAll(new NetworkBattleStartRequest(
                Guid.NewGuid().ToString(),
                (int)BattleStartMode.Mission,
                mapEventId,
                partyIds[0])), MapEventDisabledMethods);

            Assert.True(Server.NetworkSentMessages.GetMessages<NetworkBattleStartReply>().Single().Accepted);
            Assert.Equal(2, Server.NetworkSentMessages.GetMessages<NetworkStartAttackMission>().Count());
            var membershipAndStarts = Server.NetworkSentMessages
                .Where(message => message is NetworkAddBattleParty or NetworkStartAttackMission)
                .ToArray();
            Assert.Collection(
                membershipAndStarts,
                message => Assert.IsType<NetworkAddBattleParty>(message),
                message => Assert.IsType<NetworkStartAttackMission>(message),
                message => Assert.IsType<NetworkAddBattleParty>(message),
                message => Assert.IsType<NetworkStartAttackMission>(message));
            Assert.Equal(
                2,
                Server.NetworkSentMessages.GetMessages<NetworkAddBattleParty>()
                    .Select(message => message.MapEventPartyId)
                    .Distinct()
                    .Count());
            Assert.Equal(
                new[] { 1, 1, 0 },
                clients.Select(client => client.InternalMessages.GetMessageCount<NetworkStartAttackMission>()));
            clients[1].Call(() =>
            {
                Assert.True(clients[1].ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
                Assert.True(clients[1].ObjectManager.TryGetObject<MobileParty>(partyIds[1], out var participant));
                Assert.Contains(
                    mapEvent.DefenderSide.Parties,
                    party => party.Party == participant.Party);
            });
            Assert.Equal(
                new[] { "ctrl-A", "ctrl-B" },
                Server.InternalMessages.GetMessages<BattleJoinAccepted>()
                    .Where(payload => payload.InstanceId == mapEventId)
                    .Select(payload => payload.ControllerId)
                    .OrderBy(id => id));
        }
        finally
        {
            Server.Call(() => ServerBattleModeArbiter.Release(mapEventId));
        }
    }

    [Fact]
    [Trait("Requirement", "BR-004")]
    public void AuthoritativeJoin_CanHostAndReceivesOwnReserve()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B");
        var clients = Clients.ToArray();
        var joiningPartyId = CreateRegisteredObject<MobileParty>(MapEventDisabledMethods);
        var joiningHeroId = CreateRegisteredObject<Hero>();
        var troopId = CreateRegisteredObject<CharacterObject>();
        SetControllerId(clients[2], "ctrl-Joiner");
        RegisterAsPlayerParty("ctrl-Joiner", joiningHeroId, joiningPartyId);
        Server.Resolve<IPlayerManager>().SetPeer("ctrl-Joiner", clients[2].NetPeer);

        clients[2].NetworkSentMessages.Clear();
        Server.NetworkSentMessages.Clear();
        clients[2].Call(() =>
        {
            Assert.True(clients[2].ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(clients[2].ObjectManager.TryGetObject<MobileParty>(joiningPartyId, out var joiningParty));
            joiningParty.Party.MapEventSide = mapEvent.AttackerSide;
        }, MapEventDisabledMethods);

        Assert.True(Server.NetworkSentMessages.GetMessages<NetworkJoinBattleReply>().Single().Accepted);

        string? joiningMapEventPartyId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(joiningPartyId, out var joiningParty));
            Assert.True(Server.ObjectManager.TryGetObject<CharacterObject>(troopId, out var troop));
            var mapEventParty = Assert.Single(
                mapEvent.AttackerSide.Parties,
                party => party.Party == joiningParty.Party);

            joiningParty.MemberRoster.AddToCounts(troop, 3);
            mapEventParty.Update();
            Assert.True(Server.ObjectManager.TryGetId(mapEventParty, out joiningMapEventPartyId));
        }, MapEventDisabledMethods);

        foreach (var instance in new[] { Server }.Concat(Clients))
        {
            instance.Call(() =>
            {
                Assert.True(instance.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
                Assert.True(instance.ObjectManager.TryGetObject<MobileParty>(joiningPartyId, out var joiningParty));
                Assert.Single(mapEvent.AttackerSide.Parties, party => party.Party == joiningParty.Party);
            });
        }

        EnterBattle(clients[2], mapEventId);

        AssertHost(Server, mapEventId, "ctrl-Joiner");
        foreach (var client in Clients)
            AssertHost(client, mapEventId, "ctrl-Joiner");

        var joiningReserves = clients[2].InternalMessages.GetMessages<NetworkBattleTroopReserve>()
            .Where(message => message.MapEventId == mapEventId)
            .SelectMany(message => message.Parties)
            .Where(party => party.PartyId == joiningMapEventPartyId)
            .ToArray();
        Assert.NotEmpty(joiningReserves);
        Assert.All(joiningReserves, reserve => Assert.True(reserve.IsReceiverPlayerParty));
    }
}
