using Common.Messaging;
using Common.Network.Messages;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.Instances;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.MapEvents.TroopSupply.Messages;
using GameInterface.Services.Players;
using Missions.Messages;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// BR-017 (Abandoned Battle Resolution): "If no players remain in an active battle mission — through
/// disconnection, retreat, or leaving — the server shall destroy the battle instance."
/// <para>
/// Destroying the battle INSTANCE means the server-side instance record is fully torn down: the host
/// assignment is removed, the battle's troop reserves are forgotten, the mission-mode claim is released
/// (broadcast as Unclaimed), and the mission-membership record is pruned. The MAP EVENT itself persists,
/// reflecting the last synchronized battle state (the casualty-synced rosters), unresolved and unfinalized —
/// it remains available for resolution at the players' discretion: a re-engaging player starts a NEW battle
/// mission (BR-054/BR-002, with a fresh host election per BR-102), or players choose player simulation —
/// BR-003's mission/simulation mutual exclusion resets when the instance is destroyed.
/// </para>
/// </summary>
public class BattleAbandonmentTests : MissionTestEnvironment
{
    public BattleAbandonmentTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void DisconnectAndReconnectToAnUnresolvedMapEvent_RefreshesTheFastForwardLock()
    {
        var (mapEventId, _) = SetupCoopBattle("connected-ctrl", "offline-ctrl");
        var connected = Clients.First();

        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            playerManager.SetPeer("connected-ctrl", connected.NetPeer);
            Assert.True(playerManager.TryGetPlayer("connected-ctrl", out var player));
            Assert.True(playerManager.IsConnected(player));
            Server.Resolve<IMessageBroker>().Publish(this, new PlayerJoinedBattle());
        });

        Assert.Equal(1, Server.NetworkSentMessages.GetMessages<NetworkMapEventLockChanged>().Last().PlayersInMapEvent);

        Server.Call(() =>
        {
            Server.Resolve<IPlayerManager>().ClearPeer(connected.NetPeer);
            Server.Resolve<IMessageBroker>().Publish(this, new PlayerDisconnected(connected.NetPeer, default));
        });

        Assert.Equal(0, Server.NetworkSentMessages.GetMessages<NetworkMapEventLockChanged>().Last().PlayersInMapEvent);
        Server.Call(() => Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out _)));

        Server.NetworkSentMessages.Clear();
        Server.Call(() =>
        {
            var playerManager = Server.Resolve<IPlayerManager>();
            playerManager.SetPeer("connected-ctrl", connected.NetPeer);
            Server.Resolve<ITimeControlInterface>().ServerSetTimeControl(TimeControlEnum.Play_2x);
            Server.Resolve<IMessageBroker>().Publish(this, new PlayerCampaignEntered(connected.NetPeer));
        });

        Assert.Equal(1, Server.NetworkSentMessages.GetMessages<NetworkMapEventLockChanged>().Last().PlayersInMapEvent);
        Server.Call(() => Assert.Equal(TimeControlEnum.Play_1x,
            Server.Resolve<ITimeControlInterface>().GetTimeControl()));
    }

    /// <summary>
    /// The last player DISCONNECTS from an active battle in which casualties were already synced. The battle
    /// instance is destroyed (no host assignment, no reserves, mode released to Unclaimed) while the map
    /// event persists at its last synchronized state: still alive on every instance, BattleState unresolved,
    /// the synced casualties neither restored nor extended, and nothing finalized.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-017")]
    public void LastPlayerDisconnects_DestroysBattleInstance_MapEventPersistsAtLastSyncedState()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B");
        var clients = Clients.ToArray();

        // Seed the defender party with five identical troops BEFORE any reserve is flattened, so the
        // casualty pipeline below has a known population to hit.
        var troop = Server.CreateRegisteredObject<CharacterObject>("abandonment_troop");
        string defenderPartyId = null;
        string troopId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            var defenderParty = mapEvent.DefenderSide.Parties[0];
            Assert.True(Server.ObjectManager.TryGetId(defenderParty, out defenderPartyId));
            Assert.True(Server.ObjectManager.TryGetId(troop, out troopId));

            defenderParty.Party.MemberRoster.AddToCounts(troop, 5);
            defenderParty.Update();
            Assert.Equal(5, CountAvailable(defenderParty.Troops, troop));
        });

        EnterBattle(clients[0], mapEventId); // ctrl-A first mission-ready -> host
        EnterBattle(clients[1], mapEventId); // ctrl-B -> successor
        AssertHost(Server, mapEventId, "ctrl-A", "ctrl-B");

        // The server granted the live mission (BR-001): the event is claimed for the Mission mode.
        Server.Call(() => Assert.True(ServerBattleModeArbiter.TryClaimMission(mapEventId)));

        // Mid-battle casualties synced through the real casualty pipeline: two killed, one wounded. This is
        // the "last synchronized battle state" the abandoned map event must retain.
        Server.Call(() =>
        {
            var broker = Server.Resolve<IMessageBroker>();
            broker.Publish(this, new NetworkRequestBattleCasualty(defenderPartyId, troopId, wounded: false));
            broker.Publish(this, new NetworkRequestBattleCasualty(defenderPartyId, troopId, wounded: false));
            broker.Publish(this, new NetworkRequestBattleCasualty(defenderPartyId, troopId, wounded: true));

            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.Equal(2, CountAvailable(mapEvent.DefenderSide.Parties[0].Troops, troop));
        });

        Server.NetworkSentMessages.Clear();

        // The host drops (disconnect, not retreat) -> migration promotes ctrl-B; then the promoted host
        // drops too, emptying the instance.
        DepartBattle("ctrl-A", mapEventId, wasRetreat: false, isInstanceEmpty: false);
        DepartBattle("ctrl-B", mapEventId, wasRetreat: false, isInstanceEmpty: true);

        AssertBattleInstanceDestroyed(mapEventId);

        // The map event PERSISTS at the last synchronized state: alive everywhere, unresolved, casualties
        // retained exactly (nothing restored by the teardown, nothing resolved after it).
        AssertMapEventPersistsUnresolved(mapEventId);
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.Equal(2, CountAvailable(mapEvent.DefenderSide.Parties[0].Troops, troop));
        });
    }

    /// <summary>
    /// The server observes the instance become empty on a NON-host departure — e.g. the host's own departure
    /// was lost, so the successor's leave is the one that empties the instance. BR-017 keys on "no players
    /// remain", not on who the last leaver was: the battle instance must still be destroyed in full (host
    /// assignment removed and reserves forgotten, not just the mode release).
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-017")]
    public void LastNonHostPlayerDeparts_InstanceEmpty_StillDestroysBattleInstance()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B");
        var clients = Clients.ToArray();
        EnterBattle(clients[0], mapEventId); // ctrl-A -> host
        EnterBattle(clients[1], mapEventId); // ctrl-B -> successor
        AssertHost(Server, mapEventId, "ctrl-A", "ctrl-B");

        Server.Call(() => Assert.True(ServerBattleModeArbiter.TryClaimMission(mapEventId)));

        // Seed a reserve keyed by a party neither controller owns, so only a whole-battle forget
        // (ForgetMapEvent) can clear it — per-controller cleanup would leave it behind.
        Server.Call(() => Server.Resolve<IBattleTroopLedger>()
            .SetReserve(mapEventId, "abandoned-reserve-party", new[] { new TroopReserveEntry(4242, "char-y", 0) }));

        Server.NetworkSentMessages.Clear();

        // Only the successor's departure reaches the server, and it empties the instance (the host's own
        // departure never arrived).
        DepartBattle("ctrl-B", mapEventId, wasRetreat: false, isInstanceEmpty: true);

        AssertBattleInstanceDestroyed(mapEventId);
        AssertMapEventPersistsUnresolved(mapEventId);
    }

    /// <summary>
    /// The last player RETREATS (graceful leave) with the map event still unresolved: same destruction of
    /// the battle instance, same persistence of the unresolved map event (BR-054 hands off to BR-017).
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-017")]
    public void LastPlayerRetreats_DestroysBattleInstance_MapEventPersists()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B");
        var clients = Clients.ToArray();

        // Only ctrl-A ever becomes mission-ready (ctrl-B's party is in the event, but the player never
        // enters the mission), so ctrl-A's retreat empties the instance.
        EnterBattle(clients[0], mapEventId);
        AssertHost(Server, mapEventId, "ctrl-A");

        Server.Call(() => Assert.True(ServerBattleModeArbiter.TryClaimMission(mapEventId)));
        Server.Call(() => Server.Resolve<IBattleTroopLedger>()
            .SetReserve(mapEventId, "retreat-reserve-party", new[] { new TroopReserveEntry(4243, "char-z", 0) }));

        Server.NetworkSentMessages.Clear();

        DepartBattle("ctrl-A", mapEventId, wasRetreat: true, isInstanceEmpty: true);

        AssertBattleInstanceDestroyed(mapEventId);
        AssertMapEventPersistsUnresolved(mapEventId);
    }

    /// <summary>
    /// After abandonment the map event remains available for resolution at the players' discretion:
    /// (a) BR-003's exclusion RESET — a player-simulation claim for the same event is accepted; and
    /// (b) re-engagement — a later Mission claim is accepted and a NEW battle mission elects a fresh host
    /// at a HIGHER epoch than any earlier hosting generation (BR-002/BR-054/BR-102).
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-017")]
    public void AbandonedBattle_ReopensForSimulation_AndFreshMissionElection()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B");
        var clients = Clients.ToArray();
        EnterBattle(clients[0], mapEventId); // ctrl-A -> host (epoch 1)
        EnterBattle(clients[1], mapEventId); // ctrl-B -> successor
        Server.Call(() => Assert.True(ServerBattleModeArbiter.TryClaimMission(mapEventId)));

        // The host drops -> ctrl-B is promoted, opening a later hosting generation. Capture the highest
        // epoch the abandoned battle reached; the re-election below must exceed it.
        DepartBattle("ctrl-A", mapEventId);
        int abandonedEpoch = 0;
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IBattleHostRegistry>().TryGet(mapEventId, out var promoted));
            abandonedEpoch = promoted.Epoch;
        });
        Assert.True(abandonedEpoch > 0);

        // The promoted host drops too: the instance is empty, the battle instance is destroyed.
        DepartBattle("ctrl-B", mapEventId, wasRetreat: false, isInstanceEmpty: true);
        AssertBattleInstanceDestroyed(mapEventId);

        // (a) BR-003 reset: with the instance destroyed, player simulation for this event is available again.
        Server.Call(() =>
        {
            Assert.True(ServerBattleModeArbiter.TryClaimSimulation(mapEventId),
                "player simulation should be claimable after the battle instance was destroyed");
            ServerBattleModeArbiter.Release(mapEventId);
        });

        // (b) Re-engagement: a new Mission claim is accepted, and the new battle mission elects a fresh
        // host at a strictly higher epoch than the abandoned battle ever issued.
        Server.Call(() =>
        {
            Assert.True(ServerBattleModeArbiter.TryClaimMission(mapEventId),
                "a new battle mission should be claimable after the battle instance was destroyed");
        });

        EnterBattle(clients[0], mapEventId); // re-engage -> fresh election
        AssertHost(Server, mapEventId, "ctrl-A");
        foreach (var client in Clients)
            AssertHost(client, mapEventId, "ctrl-A");

        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IBattleHostRegistry>().TryGet(mapEventId, out var reElected));
            Assert.True(reElected.Epoch > abandonedEpoch,
                $"re-election epoch {reElected.Epoch} should exceed the abandoned battle's last epoch {abandonedEpoch}");

            // Static-arbiter hygiene: release the claim this test opened.
            ServerBattleModeArbiter.Release(mapEventId);
        });
    }

    /// <summary>
    /// A duplicate empty-instance departure (re-delivered leave, or a leave racing a disconnect) is harmless:
    /// the teardown is idempotent, the mode release/broadcast happens exactly once, and the map event is
    /// still alive and unresolved afterwards.
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-017")]
    public void DuplicateEmptyDeparture_TeardownIsIdempotent()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B");
        var clients = Clients.ToArray();
        EnterBattle(clients[0], mapEventId);
        Server.Call(() => Assert.True(ServerBattleModeArbiter.TryClaimMission(mapEventId)));

        Server.NetworkSentMessages.Clear();

        DepartBattle("ctrl-A", mapEventId, wasRetreat: true, isInstanceEmpty: true);
        DepartBattle("ctrl-A", mapEventId, wasRetreat: true, isInstanceEmpty: true); // duplicate

        AssertBattleInstanceDestroyed(mapEventId);
        AssertMapEventPersistsUnresolved(mapEventId);
    }

    /// <summary>
    /// The full membership-driven path: battle members announce entry/leave through the real relay
    /// membership messages (<see cref="NetworkMissionEntered"/>/<see cref="NetworkMissionLeft"/>, instance id
    /// = map event id, as <c>BattleInstanceLifecycle</c> announces them). When the last member leaves, the
    /// server's mission-instance record itself is pruned — "destroy the battle instance" includes the
    /// membership/relay record, which previously leaked per battle — and the same departure drives the
    /// battle teardown (assignment + reserves + mode).
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-017")]
    public void MembershipDrivenAbandonment_PrunesInstanceRecord_AndTearsDownBattle()
    {
        var (mapEventId, _) = SetupCoopBattle("ctrl-A", "ctrl-B");
        var clients = Clients.ToArray();
        EnterBattle(clients[0], mapEventId);
        EnterBattle(clients[1], mapEventId);
        Server.Call(() => Assert.True(ServerBattleModeArbiter.TryClaimMission(mapEventId)));

        // Both members announce entry into the battle's mission instance over the relay.
        Server.SimulateMessage(clients[0].NetPeer, new NetworkMissionEntered("ctrl-A", mapEventId));
        Server.SimulateMessage(clients[1].NetPeer, new NetworkMissionEntered("ctrl-B", mapEventId));
        Server.Call(() =>
        {
            Assert.True(Server.Resolve<IMissionManager>().TryGetControllers(mapEventId, out var controllers));
            Assert.Equal(2, controllers.Count);
        });

        // Both leave; the second leave empties the instance and must destroy it — including pruning the
        // server's membership record for the instance.
        Server.SimulateMessage(clients[0].NetPeer, new NetworkMissionLeft("ctrl-A", mapEventId));
        Server.SimulateMessage(clients[1].NetPeer, new NetworkMissionLeft("ctrl-B", mapEventId));

        Server.Call(() =>
        {
            Assert.False(Server.Resolve<IMissionManager>().TryGetControllers(mapEventId, out _),
                "the empty mission-instance record should be pruned when its last member leaves");
        });
        AssertBattleInstanceDestroyed(mapEventId);
        AssertMapEventPersistsUnresolved(mapEventId);

        // The disconnect path prunes too: a member re-enters (recreating the record), then drops ungracefully.
        Server.SimulateMessage(clients[0].NetPeer, new NetworkMissionEntered("ctrl-A", mapEventId));
        Server.Call(() =>
        {
            var missionManager = Server.Resolve<IMissionManager>();
            Assert.True(missionManager.TryGetControllers(mapEventId, out _));

            Assert.True(missionManager.TryHandleDisconnect(clients[0].NetPeer, out var controllerId, out var instanceId, out var remaining));
            Assert.Equal("ctrl-A", controllerId);
            Assert.Equal(mapEventId, instanceId);
            Assert.Empty(remaining);

            Assert.False(missionManager.TryGetControllers(mapEventId, out _),
                "the empty mission-instance record should be pruned when its last member disconnects");
        });
    }

    /// <summary>
    /// A member is still LOADING (entered, not yet mission-ready) when the elected host DISCONNECTS with no
    /// mission-ready successor. The instance is NOT empty, so this is not an abandonment (BR-017): the departed
    /// host must stay ABSENT-marked so that the still-loading player, once it becomes the eventual first-ready
    /// host, inherits the disconnected host's party into its reserve scope. (The reserves themselves are still
    /// re-flattened — with no surviving ready client nothing persists on the field — but the absent bookkeeping
    /// must survive, which is exactly what the no-successors branch was clearing.)
    /// <para>
    /// PRE-FIX FAILURE MECHANISM: the no-successors host-departure branch ran <c>ClearBattleRecords</c> even
    /// though the instance was not empty, dropping the disconnected host's absent marker. When the loading
    /// player is then elected host, the reserve build resolves the disconnected host's (attacker-side) party to
    /// that still-registered — and no-longer-absent — host, not to the new host, so the grant omits it: the new
    /// host's feeds never carry the disconnected host's MapEventParty id and the final <c>Assert.Contains</c>
    /// fails. Keeping the absent marker (skipping <c>ClearBattleRecords</c> unless the instance is truly empty)
    /// makes the party fall to the eventual host.
    /// </para>
    /// </summary>
    [Fact]
    [Trait("Requirement", "BR-017")]
    public void HostDisconnectsWithNoReadySuccessor_WhilePlayerStillLoading_KeepsDepartedHostAbsentForEventualHost()
    {
        var (mapEventId, partyIds) = SetupCoopBattle("host-ctrl", "loader-ctrl");
        var clients = Clients.ToArray();

        EnterBattle(clients[0], mapEventId);                      // host-ctrl -> host (mission-ready)
        EnterBattle(clients[1], mapEventId, missionReady: false); // loader-ctrl entered but still loading
        AssertHost(Server, mapEventId, "host-ctrl");              // no successor — the loader is not ready

        var hostPartyMep = GetMapEventPartyId(mapEventId, partyIds[0]);

        Server.NetworkSentMessages.Clear();

        // The elected host disconnects; no ready successor exists, but the loader is still in the instance.
        DepartBattle("host-ctrl", mapEventId, wasRetreat: false, isInstanceEmpty: false);

        // The now-hostless assignment is removed, but the battle is NOT announced torn down — no Unclaimed mode
        // is broadcast (that happens only on an observed-empty instance).
        Server.Call(() => Assert.False(Server.Resolve<IBattleHostRegistry>().TryGet(mapEventId, out _),
            "the departed host's now-hostless assignment should be removed"));
        Assert.Empty(Server.NetworkSentMessages.GetMessages<NetworkBattleModeSet>());

        // The still-loading player becomes ready: it is elected as the eventual (fresh) host and must inherit
        // the disconnected host's party — the departed host stayed absent, so its (attacker-side) party falls to
        // the new host's reserve scope rather than resolving back to the still-registered departed host.
        var loader = clients[1];
        int loaderBaseline = loader.InternalMessages.GetMessages<NetworkBattleTroopReserve>()
            .Count(message => message.MapEventId == mapEventId);
        MakeMissionReady(loader, mapEventId);
        AssertHost(Server, mapEventId, "loader-ctrl");

        var grantedParties = loader.InternalMessages.GetMessages<NetworkBattleTroopReserve>()
            .Where(message => message.MapEventId == mapEventId)
            .Skip(loaderBaseline)
            .SelectMany(feed => feed.Parties)
            .Select(party => party.PartyId);
        Assert.Contains(hostPartyMep, grantedParties);
    }

    // ------------------------------------------------------------------
    // Shared asserts
    // ------------------------------------------------------------------

    /// <summary>
    /// Asserts the battle INSTANCE is destroyed on the server: no host assignment remains, the battle's
    /// troop reserves are forgotten, and the mission-mode claim was released — broadcast to the clients as
    /// exactly one Unclaimed mode set (so a duplicate teardown cannot double-broadcast).
    /// </summary>
    private void AssertBattleInstanceDestroyed(string mapEventId)
    {
        Server.Call(() =>
        {
            Assert.False(Server.Resolve<IBattleHostRegistry>().TryGet(mapEventId, out _),
                "no host assignment should remain for a destroyed battle instance");
            Assert.Empty(Server.Resolve<IBattleTroopLedger>().GetParties(mapEventId));

            // Unclaimed again: a fresh Mission claim must succeed (proving the mission claim is gone) —
            // and is immediately released to leave the arbiter untouched.
            Assert.True(ServerBattleModeArbiter.TryClaimMission(mapEventId));
            ServerBattleModeArbiter.Release(mapEventId);
        });

        var modeChange = Assert.Single(Server.NetworkSentMessages.GetMessages<NetworkBattleModeSet>());
        Assert.Equal(mapEventId, modeChange.MapEventId);
        Assert.Equal((int)BattleStartMode.Unclaimed, modeChange.Mode);
    }

    /// <summary>
    /// Asserts the map event PERSISTS unresolved after the abandonment: alive on the server and every
    /// client, BattleState still undecided, and no finalize/conclusion ever ran on the server.
    /// </summary>
    private void AssertMapEventPersistsUnresolved(string mapEventId)
    {
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent),
                "the abandoned map event must persist on the server");
            Assert.Equal(BattleState.None, mapEvent.BattleState);
            Assert.False(mapEvent.IsFinalized, "the abandoned map event must not be finalized");
        });

        foreach (var client in Clients)
        {
            client.Call(() =>
            {
                Assert.True(client.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent),
                    "the abandoned map event must persist on every client");
                Assert.Equal(BattleState.None, mapEvent.BattleState);
            });
        }

        // Nothing resolved or finalized the event server-side: no conclusion, no finalize attempt, and
        // FinalizeEventAux never ran (its server postfix would have published MapEventFinalized).
        Assert.Empty(Server.InternalMessages.GetMessages<MapEventConcluded>());
        Assert.Empty(Server.InternalMessages.GetMessages<MapEventFinalizeAttempted>());
        Assert.Empty(Server.InternalMessages.GetMessages<MapEventFinalized>());
    }

    /// <summary>Counts troops of <paramref name="troop"/> still available to fight in a flattened battle
    /// roster (killed/wounded/routed excluded, exactly as reserve rebuilds exclude them).</summary>
    private static int CountAvailable(FlattenedTroopRoster roster, CharacterObject troop)
    {
        int n = 0;
        foreach (var element in roster)
            if (!element.IsKilled && !element.IsWounded && !element.IsRouted && element.Troop == troop) n++;
        return n;
    }

    /// <summary>The MapEventParty id wrapping a player's party — the key the reserve ledger uses.</summary>
    private string GetMapEventPartyId(string mapEventId, string partyId)
    {
        string mepId = null;
        Server.Call(() =>
        {
            Assert.True(Server.ObjectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent));
            Assert.True(Server.ObjectManager.TryGetObject<MobileParty>(partyId, out var party));
            var mep = mapEvent.AttackerSide.Parties.Concat(mapEvent.DefenderSide.Parties)
                .Last(p => p.Party == party.Party);
            Assert.True(Server.ObjectManager.TryGetId(mep, out mepId));
        }, MapEventDisabledMethods);
        Assert.NotNull(mepId);
        return mepId;
    }
}
