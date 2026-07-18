using System.Linq;
using Common.Network;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.MapEvents.TroopSupply.Messages;
using GameInterface.Services.Players;
using Missions.Battles;
using Missions.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// End-to-end coverage of MISSION-READY host election (BR-010) and the mission-ready connection order
/// (BR-013): the first player to FINISH LOADING the battle mission — not the first to open it — becomes the
/// mission host, as observed by the campaign server. A player that has entered but is still on the loading
/// screen has not yet joined for ordering purposes, and the unowned (NPC) troop reserves are issued to the
/// elected host together with the election, while a ready non-host receives its sides including explicit
/// empties so its spawn sizing can proceed.
/// </summary>
public class MissionReadyElectionTests : MissionTestEnvironment
{
    public MissionReadyElectionTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    /// <summary>
    /// BR-010: two players open the battle in order A,B but finish loading in order B,A. Entry alone elects
    /// no one (both are still on the loading screen); the first MISSION-READY player (B) becomes the host on
    /// the server and every client, with A behind it in the successor line.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-010")]
    public void MissionReadyOrder_NotEntryOrder_ElectsTheHost()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B");
        var clients = Clients.ToArray();

        EnterBattle(clients[0], mapEventId, missionReady: false); // ctrl-A opens the mission first...
        EnterBattle(clients[1], mapEventId, missionReady: false); // ...then ctrl-B; both still loading

        // BR-010: a player on the loading screen has not joined — entry alone must not elect anyone.
        AssertNoHost(Server, mapEventId);

        MakeMissionReady(clients[1], mapEventId); // ctrl-B finishes loading FIRST -> host
        MakeMissionReady(clients[0], mapEventId); // ctrl-A finishes loading second -> successor

        AssertHost(Server, mapEventId, "ctrl-B", "ctrl-A");
        foreach (var client in Clients)
            AssertHost(client, mapEventId, "ctrl-B", "ctrl-A");
        AssertIsLocalHost(clients[1], mapEventId, true);
        AssertIsLocalHost(clients[0], mapEventId, false);
    }

    /// <summary>
    /// BR-013: the server's per-battle connection order contains only MISSION-READY players. A player that
    /// has entered (opened the mission) but not finished loading does not appear in the successor line; it
    /// appends — at the tail — once it becomes ready.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-013")]
    public void EnteredButNotReadyPlayer_IsNotInConnectionOrder_AppendsWhenReady()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B", "ctrl-C");
        var clients = Clients.ToArray();

        EnterBattle(clients[0], mapEventId); // ctrl-A entered + ready -> host
        EnterBattle(clients[1], mapEventId); // ctrl-B entered + ready -> successor
        EnterBattle(clients[2], mapEventId, missionReady: false); // ctrl-C is still on the loading screen

        // ctrl-C has not joined yet (BR-013): it must not be in the connection order anywhere.
        AssertHost(Server, mapEventId, "ctrl-A", "ctrl-B");
        foreach (var client in Clients)
            AssertHost(client, mapEventId, "ctrl-A", "ctrl-B");

        MakeMissionReady(clients[2], mapEventId); // ctrl-C finishes loading -> joins at the tail

        AssertHost(Server, mapEventId, "ctrl-A", "ctrl-B", "ctrl-C");
        foreach (var client in Clients)
            AssertHost(client, mapEventId, "ctrl-A", "ctrl-B", "ctrl-C");
    }

    [Fact]
    [Trait("Requirement", "BR-010")]
    public void EnteredButNotReadyPlayer_RetainsItsReserveWhileTheFirstHostIsElected()
    {
        var (mapEventId, partyIds) = SetupCoopBattle("host-ctrl", "loader-ctrl");
        var clients = Clients.ToArray();
        var host = clients[0];
        var loader = clients[1];
        string loaderMapEventPartyId = null;

        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyIds[1], out var loaderParty));
            var mapEventParty = mapEvent.PartiesOnSide(loaderParty.Party.Side)
                .Single(party => party.Party == loaderParty.Party);
            Assert.True(Server.ObjectManager.TryGetId(mapEventParty, out loaderMapEventPartyId));
        });

        EnterBattle(loader, mapEventId, missionReady: false);
        EnterBattle(host, mapEventId, missionReady: false);
        int baseline = host.InternalMessages.GetMessages<NetworkBattleTroopReserve>()
            .Count(message => message.MapEventId == mapEventId);

        MakeMissionReady(host, mapEventId);

        var hostElectionFeeds = host.InternalMessages.GetMessages<NetworkBattleTroopReserve>()
            .Where(message => message.MapEventId == mapEventId)
            .Skip(baseline)
            .ToArray();
        Assert.DoesNotContain(hostElectionFeeds.SelectMany(feed => feed.Parties),
            party => party.PartyId == loaderMapEventPartyId);
    }

    /// <summary>
    /// BR-013/BR-014: host migration promotes down the MISSION-READY order, not the entry order. Entry order
    /// is A,B,C but ready order is C,B,A, so the connection order is C,B,A and successive host departures
    /// promote B then A.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-013")]
    public void SuccessorPromotion_FollowsMissionReadyOrder_NotEntryOrder()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B", "ctrl-C");
        var clients = Clients.ToArray();

        EnterBattle(clients[0], mapEventId, missionReady: false); // entry order: A...
        EnterBattle(clients[1], mapEventId, missionReady: false); // ...B...
        EnterBattle(clients[2], mapEventId, missionReady: false); // ...C

        MakeMissionReady(clients[2], mapEventId); // ready order: C (host)...
        MakeMissionReady(clients[1], mapEventId); // ...B...
        MakeMissionReady(clients[0], mapEventId); // ...A

        AssertHost(Server, mapEventId, "ctrl-C", "ctrl-B", "ctrl-A");

        DepartBattle("ctrl-C", mapEventId); // the host leaves -> promote by ready order: B, not entry-first A
        AssertHost(Server, mapEventId, "ctrl-B", "ctrl-A");
        foreach (var client in Clients)
            AssertHost(client, mapEventId, "ctrl-B", "ctrl-A");

        DepartBattle("ctrl-B", mapEventId); // next host leaves -> A is last
        AssertHost(Server, mapEventId, "ctrl-A");
        foreach (var client in Clients)
            AssertHost(client, mapEventId, "ctrl-A");
    }

    /// <summary>
    /// BR-010: the unowned (NPC) troop reserves are issued to the elected host TOGETHER with the election —
    /// not at entry — and a ready non-host receives its sides including an explicit empty enemy side so its
    /// spawn sizing can proceed. At entry a client receives ONLY sides that contain parties it owns: an empty
    /// unowned side must not arrive early and mark the enemy-side supplier populated before the election.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-010")]
    public void Election_IssuesNpcReservesToHost_AndExplicitEmptySidesToReadyNonHost()
    {
        // "host-ctrl" = attacker side, "other-ctrl" = defender side.
        var (mapEventId, _) = SetupCoopBattle("host-ctrl", "other-ctrl");
        var clients = Clients.ToArray();

        // The supplier registry is process-static and map-event ids can repeat across sequential tests, so
        // drop any reserve another test buffered under this id before observing feeds through it here.
        CoopTroopSupplierRegistry.ClearBattle(mapEventId);

        // An unowned (NPC) party with a known roster on the DEFENDER side — no player owns it, so its reserve
        // belongs to whoever is elected host.
        var aiPartyId = CreateRegisteredObject<MobileParty>();
        string aiMepId = null;      // the NPC MapEventParty id (the reserve key)
        string hostMepId = null;    // the host's own MapEventParty id
        string otherMepId = null;   // the non-host player's MapEventParty id
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(aiPartyId, out var aiParty));

            aiParty.Party.MapEventSide = mapEvent.DefenderSide;
            var aiMep = mapEvent.DefenderSide.Parties.Last(p => p.Party == aiParty.Party);
            var npcTroop = Server.CreateRegisteredObject<CharacterObject>("br010_npc_troop");
            aiMep.Party.MemberRoster.Clear();
            aiMep.Party.MemberRoster.AddToCounts(npcTroop, 3);
            aiMep.Update();
            Assert.True(Server.ObjectManager.TryGetId(aiMep, out aiMepId));

            Assert.True(Server.ObjectManager.TryGetId(mapEvent.AttackerSide.Parties[0], out hostMepId));
            Assert.True(Server.ObjectManager.TryGetId(mapEvent.DefenderSide.Parties[0], out otherMepId));
        }, MapEventDisabledMethods);

        // ---- Phase 1: the future host enters, then becomes ready (elected). ----
        var hostAttacker = new CoopTroopSupplier(mapEventId, BattleSideEnum.Attacker, null);
        var hostDefender = new CoopTroopSupplier(mapEventId, BattleSideEnum.Defender, null);
        CoopTroopSupplierRegistry.Register(hostAttacker);
        CoopTroopSupplierRegistry.Register(hostDefender);
        try
        {
            Server.Call(() =>
            {
                var playerManager = Server.Resolve<IPlayerManager>();
                playerManager.SetPeer("other-ctrl", clients[1].NetPeer);
                Assert.True(playerManager.TryGetPlayer("other-ctrl", out var otherPlayer));
                Assert.True(playerManager.IsConnected(otherPlayer));
            });

            EnterBattle(clients[0], mapEventId, missionReady: false);

            // Entry: only the owned side arrives. The unowned (defender/NPC) side must NOT arrive — not even
            // as an explicit empty — because the NPC grant is decided by the election, which has not run.
            AssertNoHost(Server, mapEventId);
            Assert.True(hostAttacker.IsPopulated, "own-side reserve should arrive at entry");
            Assert.Equal(hostMepId, Assert.Single(hostAttacker.GetSuppliedByParty()).partyId);
            Assert.False(hostDefender.IsPopulated,
                "the unowned enemy side must not be delivered (not even empty) before the election");

            MakeMissionReady(clients[0], mapEventId); // first mission-ready -> elected host

            // The election issues the NPC reserves to the elected host together with the assignment.
            AssertHost(Server, mapEventId, "host-ctrl");
            Assert.True(hostDefender.IsPopulated, "the NPC-side grant should arrive with the election");
            var granted = hostDefender.GetSuppliedByParty();
            Assert.Contains(granted, party => party.partyId == aiMepId);        // the unowned NPC party
            Assert.DoesNotContain(granted, party => party.partyId == otherMepId); // never another player's party
        }
        finally
        {
            CoopTroopSupplierRegistry.ClearBattle(mapEventId);
        }

        // ---- Phase 2: the non-host enters, then becomes ready. Fresh suppliers observe only its feeds. ----
        var otherAttacker = new CoopTroopSupplier(mapEventId, BattleSideEnum.Attacker, null);
        var otherDefender = new CoopTroopSupplier(mapEventId, BattleSideEnum.Defender, null);
        CoopTroopSupplierRegistry.Register(otherAttacker);
        CoopTroopSupplierRegistry.Register(otherDefender);
        try
        {
            EnterBattle(clients[1], mapEventId, missionReady: false);

            // Entry: its own (defender) party only — no NPC party, and no empty enemy side yet.
            Assert.True(otherDefender.IsPopulated, "own-side reserve should arrive at entry");
            Assert.Equal(otherMepId, Assert.Single(otherDefender.GetSuppliedByParty()).partyId);
            Assert.False(otherAttacker.IsPopulated,
                "the enemy side must not be delivered before this client is mission-ready");

            MakeMissionReady(clients[1], mapEventId); // ready but NOT host -> successor

            AssertHost(Server, mapEventId, "host-ctrl", "other-ctrl");

            // A ready non-host receives its sides INCLUDING the explicit empty enemy side, so both suppliers
            // are populated and its joint spawn sizing can proceed (CoopBattleMissionSpawnHandler.SideSizing).
            Assert.True(otherAttacker.IsPopulated, "the explicit empty enemy side should arrive with the election reply");
            Assert.Equal(0, otherAttacker.TotalTroops); // it owns nothing there — explicitly empty, not missing
            Assert.Equal(otherMepId, Assert.Single(otherDefender.GetSuppliedByParty()).partyId);

            var sizing = new CoopBattleMissionSpawnHandler.SideSizing(
                otherDefender.IsPopulated, otherAttacker.IsPopulated,
                otherDefender.TotalTroops, otherAttacker.TotalTroops);
            Assert.True(sizing.Ready, "both reserves landed, so the spawn handler's sizing gate must open");
        }
        finally
        {
            CoopTroopSupplierRegistry.ClearBattle(mapEventId);
        }
    }

    /// <summary>A current host's reserve refresh carries both sides so migration recovery can observe one
    /// complete snapshot even when every party it owns is on only one side.</summary>
    [Fact]
    [Trait("Requirement", "BR-031")]
    public void CurrentHostReserveRefresh_IncludesExplicitEmptySideForMigrationCompletion()
    {
        var (mapEventId, _) = SetupCoopBattle("host-ctrl", "other-ctrl");
        var clients = Clients.ToArray();
        var host = clients[0];

        EnterBattle(host, mapEventId);
        EnterBattle(clients[1], mapEventId);
        AssertHost(Server, mapEventId, "host-ctrl", "other-ctrl");

        int baseline = host.InternalMessages.GetMessages<NetworkBattleTroopReserve>()
            .Count(message => message.MapEventId == mapEventId);

        host.Call(() => host.Resolve<INetwork>().SendAll(
            new NetworkRequestBattleReserves(mapEventId, "host-ctrl")));

        var refresh = host.InternalMessages.GetMessages<NetworkBattleTroopReserve>()
            .Where(message => message.MapEventId == mapEventId)
            .Skip(baseline)
            .ToArray();

        Assert.Equal(2, refresh.Length);
        Assert.NotEmpty(Assert.Single(refresh, message => message.Side == (int)BattleSideEnum.Attacker).Parties);
        Assert.Empty(Assert.Single(refresh, message => message.Side == (int)BattleSideEnum.Defender).Parties);
    }
}
