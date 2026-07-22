using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.MapEvents.TroopSupply.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.PlayerCaptivityService.Messages;
using GameInterface.Services.Players;
using LiteNetLib;
using Missions.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace Missions.Battles;

/// <summary>
/// Server-authoritative election of the battle host (and successor order) for a field battle, plus the
/// client-side receipt of that assignment. Lives in the Missions stack and runs on both sides because
/// MissionModule is registered into the client and the server containers.
/// <para>
/// Client: on entering a battle (<see cref="PlayerEnteredBattle"/>) it only requests its OWN troop reserves
/// (<see cref="NetworkRequestBattleReserves"/>) so they feed its suppliers during the scene load; once the
/// mission has FINISHED LOADING (<see cref="BattleMissionReady"/>, published by
/// <c>CoopBattleController.AfterStart</c>) it asks the server to elect via
/// <see cref="NetworkRequestBattleHost"/>, and stores the reply (<see cref="NetworkBattleHostAssigned"/>).
/// Server: the first MISSION-READY client becomes the battle's host (BR-010); later ready clients append to
/// the successor line in mission-ready order (BR-013), so migration promotes the earliest still in the
/// mission. The unowned (NPC) reserves are issued to the elected host together with the election, and every
/// ready client's reply includes its sides with explicit empties so its spawn sizing can proceed. The result
/// is cached, so a duplicate request just re-confirms, and every change is broadcast to all clients.
/// The server also supports an explicitly non-withdrawing absence: that member is marked absent so its
/// parties fall into the host's owned set, and its RETURN re-issues the current scope to both sides of the
/// handoff — the returner's grant and a
/// shrunk refresh to the host (BR-033), so no two suppliers ever hold the same party's reserve. Because the
/// host's supplied-progress reports are throttled, the shrunk refresh carries a FLUSH handshake: the host
/// acks each flagged side with its final local pointers for the dropped parties, and the returner's grant
/// is deferred until those land in the ledger (with host-departure and deadline fallbacks so the returner
/// is never stranded) — see <see cref="HandleControllerReturn"/>.
/// </para>
/// </summary>
internal class BattleHostHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleHostHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly IBattleHostRegistry hostRegistry;
    private readonly IControllerIdProvider controllerIdProvider;
    private readonly IBattleTroopReserveBuilder reserveBuilder;
    private readonly IBattleTroopLedger ledger;
    private long nextReserveGrantGeneration;

    // [Server] Highest host epoch ever issued per battle instance (BR-102), retained across assignment
    // removal: clients keep their last assignment when a battle is fully abandoned (only the server's entry
    // is removed), so a re-election for the SAME map event must issue a HIGHER epoch than any earlier
    // generation or the clients would ignore the new election as stale. Only touched on the game thread
    // (election and migration both run under GameThread.RunSafe).
    private readonly Dictionary<string, int> issuedEpochs = new Dictionary<string, int>();

    // [Server] Per battle: members explicitly marked absent without withdrawing and not yet re-entered.
    // Player registrations survive that absence, so reserve ownership alone cannot see it — this set makes
    // the member's parties fall to the HOST's reserve scope (the reserve half of the BR-031 adoption:
    // the host's adoption-time re-request is served them at the current ledger pointers). Cleared per
    // controller on its re-entry (BR-033 — see HandleControllerReturn) and per battle on full teardown.
    // Game thread only (all mutation sites run under GameThread.RunSafe).
    private readonly Dictionary<string, HashSet<string>> absentControllers = new Dictionary<string, HashSet<string>>();

    // [Server] The current host's live peer per battle, captured from the host's own election/reserve
    // requests (the promoted host's adoption re-request refreshes it after a migration). Used to push the
    // host its SHRUNK owned set when a dropped member returns — without that refresh the host's supplier
    // would keep the returned parties and their reinforcements could be fielded twice. Validated against the
    // current assignment at use, so a stale entry (an old host that migrated away) is never used. Game thread only.
    private readonly Dictionary<string, HostEndpoint> hostPeers = new Dictionary<string, HostEndpoint>();

    // [Server] Every controller that requested reserves for a battle and its latest peer. A post-plan party can
    // belong to the host, a direct player, or a player army leader, so all active authorities need a refreshed
    // scope after the builder allocates it. Game thread only.
    private readonly Dictionary<string, Dictionary<string, NetPeer>> reservePeers =
        new Dictionary<string, Dictionary<string, NetPeer>>();

    // [Server] Exact departed human identities already considered for a priority transfer. A death and a
    // repeated rout report can name the same seed, but one human departure may transfer at most one slot.
    // Game thread only; cleared with the battle instance.
    private readonly Dictionary<string, Dictionary<string, HashSet<int>>> prioritySlotDepartures =
        new Dictionary<string, Dictionary<string, HashSet<int>>>();

    private sealed class HostEndpoint
    {
        public string ControllerId;
        public NetPeer Peer;
    }

    /// <summary>
    /// [Server] How long a deferred return grant waits for the host's flush ack (BR-033 handshake) before
    /// the returner is served from the current ledger anyway — the returner must never be stranded when
    /// the holder stops before completing the flush. Checked on the campaign tick.
    /// Internal-settable so tests can drive the deadline without wall-clock waits.
    /// </summary>
    internal TimeSpan FlushAckDeadline { get; set; } = TimeSpan.FromSeconds(2.5);

    // [Server] Per battle: return grants DEFERRED behind the shrunk host's flush acks (BR-033 handshake).
    // The ledger only holds the host's last THROTTLED progress report, so the returner must not be served
    // until the host flushed its true local pointers for the parties the shrink refresh dropped. One entry
    // per outstanding returner, completed only by acks carrying that refresh's grant generation,
    // or by the fallbacks: the host's departure serves them, the tick deadline serves them, teardown drops
    // them. Game thread only (all mutation sites run under GameThread.RunSafe or on the campaign tick).
    private readonly Dictionary<string, List<PendingReturn>> pendingReturns = new Dictionary<string, List<PendingReturn>>();

    private sealed class PendingReturn
    {
        public string MapEventId;
        public string ReturnerControllerId;
        public NetPeer ReturnerPeer;
        public string HostControllerId;
        public bool IncludeEmptySides;
        public int AwaitedAcks;
        public long GrantGeneration;
        public DateTime DeadlineUtc;
    }

    public BattleHostHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IBattleHostRegistry hostRegistry,
        IControllerIdProvider controllerIdProvider,
        IBattleTroopReserveBuilder reserveBuilder,
        IBattleTroopLedger ledger)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.hostRegistry = hostRegistry;
        this.controllerIdProvider = controllerIdProvider;
        this.reserveBuilder = reserveBuilder;
        this.ledger = ledger;

        messageBroker.Subscribe<PlayerEnteredBattle>(Handle_PlayerEnteredBattle);
        messageBroker.Subscribe<BattleMissionReady>(Handle_BattleMissionReady);
        messageBroker.Subscribe<NetworkRequestBattleHost>(Handle_NetworkRequestBattleHost);
        messageBroker.Subscribe<NetworkBattleHostAssigned>(Handle_NetworkBattleHostAssigned);
        messageBroker.Subscribe<MissionMemberDeparted>(Handle_MissionMemberDeparted);
        messageBroker.Subscribe<BattlePlayerDisconnected>(Handle_BattlePlayerDisconnected);
        messageBroker.Subscribe<NetworkRequestBattleReserves>(Handle_NetworkRequestBattleReserves);
        messageBroker.Subscribe<NetworkBattleSupplyProgress>(Handle_NetworkBattleSupplyProgress);
        messageBroker.Subscribe<BattleReserveScopeChanged>(Handle_BattleReserveScopeChanged);
        messageBroker.Subscribe<BattleHumanSlotFreed>(Handle_BattleHumanSlotFreed);
        messageBroker.Subscribe<BattlePartyLeaving>(Handle_BattlePartyLeaving);
        messageBroker.Subscribe<NetworkBattlePrioritySlotConsumed>(Handle_NetworkBattlePrioritySlotConsumed);
        messageBroker.Subscribe<NetworkBattlePrioritySlotDeclined>(Handle_NetworkBattlePrioritySlotDeclined);
        messageBroker.Subscribe<CampaignTick>(Handle_CampaignTick);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerEnteredBattle>(Handle_PlayerEnteredBattle);
        messageBroker.Unsubscribe<BattleMissionReady>(Handle_BattleMissionReady);
        messageBroker.Unsubscribe<NetworkRequestBattleHost>(Handle_NetworkRequestBattleHost);
        messageBroker.Unsubscribe<NetworkBattleHostAssigned>(Handle_NetworkBattleHostAssigned);
        messageBroker.Unsubscribe<MissionMemberDeparted>(Handle_MissionMemberDeparted);
        messageBroker.Unsubscribe<BattlePlayerDisconnected>(Handle_BattlePlayerDisconnected);
        messageBroker.Unsubscribe<NetworkRequestBattleReserves>(Handle_NetworkRequestBattleReserves);
        messageBroker.Unsubscribe<NetworkBattleSupplyProgress>(Handle_NetworkBattleSupplyProgress);
        messageBroker.Unsubscribe<BattleReserveScopeChanged>(Handle_BattleReserveScopeChanged);
        messageBroker.Unsubscribe<BattleHumanSlotFreed>(Handle_BattleHumanSlotFreed);
        messageBroker.Unsubscribe<BattlePartyLeaving>(Handle_BattlePartyLeaving);
        messageBroker.Unsubscribe<NetworkBattlePrioritySlotConsumed>(Handle_NetworkBattlePrioritySlotConsumed);
        messageBroker.Unsubscribe<NetworkBattlePrioritySlotDeclined>(Handle_NetworkBattlePrioritySlotDeclined);
        messageBroker.Unsubscribe<CampaignTick>(Handle_CampaignTick);
    }

    /// <summary>[Client] Entering a battle (still loading): request only the reserves we already OWN, so our
    /// party's troops feed the suppliers during the scene load. No election here — a player on the loading
    /// screen has not yet joined (BR-013); the unowned/NPC sides are decided by the election at mission-ready.</summary>
    private void Handle_PlayerEnteredBattle(MessagePayload<PlayerEnteredBattle> payload)
    {
        if (ModInformation.IsServer) return;

        if (!objectManager.TryGetIdWithLogging(payload.What.MapEvent, out var mapEventId))
            return;

        // On a client, SendAll targets the server (its only connected peer).
        network.SendAll(new NetworkRequestBattleReserves(mapEventId, controllerIdProvider.ControllerId));
        Logger.Information("[BattleHost] Requested own battle reserves at entry for battle {MapEventId}", mapEventId);
    }

    /// <summary>[Client] The battle mission FINISHED LOADING (we are mission-ready, BR-010): ask the server to
    /// elect (or report) the host. The server records these requests in arrival order, so its per-battle
    /// connection order is the mission-ready order (BR-013).</summary>
    private void Handle_BattleMissionReady(MessagePayload<BattleMissionReady> payload)
    {
        if (ModInformation.IsServer) return;

        var mapEventId = payload.What.MapEventId;
        if (string.IsNullOrEmpty(mapEventId)) return;

        network.SendAll(new NetworkRequestBattleHost(mapEventId, controllerIdProvider.ControllerId));
        Logger.Information("[BattleHost] Mission ready — requested host election for battle {MapEventId}", mapEventId);
    }

    /// <summary>[Server] Elect the host (first MISSION-READY requester wins, BR-010) and append later ready
    /// requesters to the successor line in arrival (= mission-ready, BR-013) order; broadcast the assignment.
    /// A duplicate/late requester just gets a re-confirm.</summary>
    private void Handle_NetworkRequestBattleHost(MessagePayload<NetworkRequestBattleHost> payload)
    {
        if (ModInformation.IsClient) return;

        var mapEventId = payload.What.MapEventId;
        var requesterId = payload.What.ControllerId;
        var requester = payload.Who as NetPeer;

        // Reads campaign collections and the shared assignment, so run on the main thread. That also
        // serializes requests for one battle: the first the server processes becomes the host, the rest
        // append in arrival (= mission-ready) order, so concurrent requests cannot double-elect.
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MapEvent>(mapEventId, out var mapEvent))
                return;

            if (string.IsNullOrEmpty(requesterId) || !IsRequesterInBattle(mapEvent, requesterId))
            {
                Logger.Warning("[BattleHost] Ignoring host request for {MapEventId} from '{Requester}' (not a participant)",
                    mapEventId, requesterId);
                return;
            }

            RememberReservePeer(mapEventId, requesterId, requester);

            if (hostRegistry.TryGet(mapEventId, out var existing))
            {
                // Host already elected. Record this player in the successor line in join order (idempotent),
                // so migration can promote the earliest joiner still present. A mid-battle joiner lands here too.
                if (TryAppendSuccessor(existing, requesterId, out var updated))
                {
                    hostRegistry.Set(mapEventId, updated);
                    Logger.Information("[BattleHost] {Requester} joined battle {MapEventId}; successor line: {Successors}",
                        requesterId, mapEventId, string.Join(", ", updated.SuccessorControllerIds));
                    network.SendAll(ToMessage(mapEventId, updated));
                }
                else if (requester != null)
                {
                    network.Send(requester, ToMessage(mapEventId, existing));
                }
            }
            else
            {
                // BR-102: the election issues the battle's next hosting generation — epoch 1 for a fresh
                // battle, or one past the last generation if this map event was abandoned and re-entered.
                var epoch = NextEpoch(mapEventId);
                var assignment = new BattleHostAssignment(requesterId, Array.Empty<string>(), epoch);
                hostRegistry.Set(mapEventId, assignment);

                Logger.Information("[BattleHost] Elected host {Host} (first mission-ready) for battle {MapEventId} at epoch {Epoch}",
                    requesterId, mapEventId, epoch);

                network.SendAll(ToMessage(mapEventId, assignment));
            }

            // A request from a member that had DROPPED is its return: re-scope the reserves (its parties
            // leave the host's scope) before serving, and refresh the shrunk holder. Normally a no-op here —
            // the entry-time reserve request (below) arrives first and already handled the return. When the
            // return's flush handshake is (still) in flight, the grant is deferred: fold this election-time
            // request into the pending grant (upgrading it to carry explicit empties) instead of serving
            // early from a ledger the host has not caught up yet.
            bool deferred = HandleControllerReturn(mapEventId, mapEvent, requesterId, requester, includeEmptySides: true);
            RememberHostPeer(mapEventId, requesterId, requester);
            if (deferred || TryDeferToPendingReturn(mapEventId, requesterId, requester, includeEmptySides: true))
                return;

            // Feed the ready client the troop reserves it owns — its own parties, plus the unowned (NPC)
            // parties when it is the elected host (BR-010: the NPC grant travels WITH the election). Empties
            // are included here so a side this client owns nothing on is explicitly final and its joint spawn
            // sizing can proceed. Buffered client-side until the supplier exists.
            SendOwnedReserves(mapEventId, mapEvent, requester, requesterId, includeEmptySides: true);
        });
    }

    /// <summary>[Server] A client asks for the reserves it currently owns: at battle ENTRY (feed its own
    /// parties while it loads), or after taking over a departed owner's troops (a host adopting a leaver, or
    /// a promoted successor) — the reply carries the full owned set at the current ledger pointers so adopted
    /// parties resume cleanly. Empty sides are skipped before the election answers, but a current host receives
    /// both sides so a migration refresh is an explicit, complete snapshot.</summary>
    private void Handle_NetworkRequestBattleReserves(MessagePayload<NetworkRequestBattleReserves> payload)
    {
        if (ModInformation.IsClient) return;

        var mapEventId = payload.What.MapEventId;
        var requesterId = payload.What.ControllerId;
        var requester = payload.Who as NetPeer;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MapEvent>(mapEventId, out var mapEvent))
                return;

            RememberReservePeer(mapEventId, requesterId, requester);
            RequeuePriorityWaitIfNeeded(mapEventId, mapEvent, requesterId);

            // A request from a member that had DROPPED is its re-entry (BR-033): take its parties back out
            // of the host's reserve scope BEFORE computing the reply — the returner's grant below must carry
            // its own parties again — and push the shrunk holder its refresh. The refresh asks the holder to
            // FLUSH its final local pointers (they can be AHEAD of its throttled reports in the ledger), and
            // while that handshake is in flight the returner's grant is DEFERRED — serving it from the
            // lagging ledger would re-issue descriptors the holder already fielded.
            bool includeEmptySides = hostRegistry.TryGet(mapEventId, out var assignment)
                && assignment.HostControllerId == requesterId;
            bool deferred = HandleControllerReturn(mapEventId, mapEvent, requesterId, requester, includeEmptySides);

            // The current host's adoption/sweep re-request also refreshes its live peer (a promoted host
            // announces itself here after a migration).
            RememberHostPeer(mapEventId, requesterId, requester);

            if (deferred || TryDeferToPendingReturn(mapEventId, requesterId, requester, includeEmptySides))
                return;

            SendEntryReserves(mapEventId, mapEvent, requester, requesterId, includeEmptySides);
        });
    }

    // [Server, game thread] A disconnected zero-entitlement joiner stays in the campaign MapEvent, so a
    // reconnect has no new involved-party add to queue it again. Its reserve request is the re-entry signal.
    private void RequeuePriorityWaitIfNeeded(string mapEventId, MapEvent mapEvent, string controllerId)
    {
        if (!TryGetControllerMapEventParty(mapEvent, controllerId, out var mapEventParty))
            return;

        reserveBuilder.GrantUnassignedInitialSpawns(
            mapEvent,
            mapEventParty,
            out _,
            out var waitsForPrioritySlot);
        if (!waitsForPrioritySlot || !objectManager.TryGetId(mapEventParty, out var partyId))
            return;

        // Broadcast before this requester's reserve reply. Both use the reliable-ordered campaign stream,
        // so every established peer reinstates the cap gate before it sees the waiting scope.
        network.SendAll(new NetworkBattlePriorityWaitQueued(
            mapEventId,
            partyId,
            resetExistingState: true));
        DrainRetainedPrioritySlots(mapEventId, mapEvent);
        Logger.Information("[BattleHost] Requeued priority wait for {PartyId} in {MapEventId} from {Controller}'s reserve request",
            partyId, mapEventId, controllerId);
    }

    private bool TryGetControllerMapEventParty(
        MapEvent mapEvent,
        string controllerId,
        out MapEventParty mapEventParty)
    {
        mapEventParty = null;
        if (!playerManager.TryGetPlayer(controllerId, out var player)
            || !objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var mobileParty))
        {
            return false;
        }

        foreach (var party in mapEvent.AttackerSide.Parties)
        {
            if (party.Party != mobileParty.Party) continue;
            mapEventParty = party;
            return true;
        }
        foreach (var party in mapEvent.DefenderSide.Parties)
        {
            if (party.Party != mobileParty.Party) continue;
            mapEventParty = party;
            return true;
        }
        return false;
    }

    /// <summary>[Server] Send the requester every reserve it owns, one message per side. With
    /// <paramref name="includeEmptySides"/> a side the requester owns nothing on is sent as an explicit empty
    /// (election/migration replies — finalizes the side so sizing proceeds); without it, empty sides are
    /// skipped (entry replies — the unowned sides are not decided until the election).</summary>
    private void SendOwnedReserves(string mapEventId, MapEvent mapEvent, NetPeer requester, string requesterId, bool includeEmptySides)
    {
        if (requester == null) return;
        SendSideReserves(requester, mapEventId,
            BuildOwnedReserves(mapEventId, mapEvent, requesterId, includeEmptySides),
            flushRequested: false,
            completesInitialSizing: includeEmptySides);
    }

    // [Server, game thread] An entrant missed earlier priority broadcasts. Replay the authoritative gate
    // before its reserve packets, then repeat it after the reserve feed so transfer application can retry.
    private void SendEntryReserves(
        string mapEventId,
        MapEvent mapEvent,
        NetPeer requester,
        string requesterId,
        bool includeEmptySides)
    {
        if (requester == null) return;

        var prioritySnapshot = reserveBuilder.GetPrioritySlotSnapshot(mapEvent);
        network.Send(requester, new NetworkBattlePrioritySnapshotReset(mapEventId));
        SendPrioritySlotSnapshot(requester, mapEventId, prioritySnapshot);
        SendOwnedReserves(mapEventId, mapEvent, requester, requesterId, includeEmptySides);
        SendPrioritySlotSnapshot(requester, mapEventId, prioritySnapshot);
    }

    private void SendPrioritySlotSnapshot(
        NetPeer requester,
        string mapEventId,
        IReadOnlyList<BattlePrioritySlotState> prioritySnapshot)
    {
        if (requester == null || prioritySnapshot == null) return;

        foreach (var state in prioritySnapshot)
        {
            if (state.TransferId <= 0)
            {
                network.Send(requester, new NetworkBattlePriorityWaitQueued(
                    mapEventId,
                    state.WaitingPartyId,
                    resetExistingState: false));
                continue;
            }

            network.Send(requester, new NetworkBattlePrioritySlotAssigned(
                mapEventId,
                state.TransferId,
                state.WaitingPartyId,
                state.DonorPartyId));
            if (state.IsConsumed)
            {
                network.Send(requester, new NetworkBattlePrioritySlotConsumed(
                    mapEventId,
                    state.TransferId,
                    state.WaitingPartyId));
            }
        }
    }

    // [Server, game thread] The per-side reserve messages a requester's grant/refresh consists of, at the
    // current ledger pointers. Split from the send so the BR-033 shrink refresh knows how many flush acks
    // to await BEFORE any flagged message goes out.
    private List<SideReserve> BuildOwnedReserves(string mapEventId, MapEvent mapEvent, string requesterId, bool includeEmptySides)
    {
        bool isHost = hostRegistry.TryGet(mapEventId, out var assignment)
            && assignment.HostControllerId == requesterId;

        // The parties of DROPPED members fall into the host's scope until they return (reserve half of the
        // BR-031 adoption); the builder treats their owners as absent.
        absentControllers.TryGetValue(mapEventId, out var absent);

        var sides = new List<SideReserve>();
        foreach (var sideReserve in reserveBuilder.GetOwnedReserves(mapEvent, requesterId, isHost, absent))
        {
            if (!includeEmptySides && (sideReserve.Parties == null || sideReserve.Parties.Length == 0))
                continue;
            sides.Add(sideReserve);
        }
        return sides;
    }

    private void SendSideReserves(
        NetPeer receiver,
        string mapEventId,
        List<SideReserve> reserves,
        bool flushRequested,
        bool completesInitialSizing,
        long grantGeneration = 0)
    {
        if (receiver == null || reserves == null || reserves.Count == 0) return;

        if (grantGeneration == 0)
            grantGeneration = ++nextReserveGrantGeneration;
        foreach (var sideReserve in reserves)
            network.Send(receiver, new NetworkBattleTroopReserve(
                mapEventId,
                (int)sideReserve.Side,
                sideReserve.Parties,
                flushRequested,
                grantGeneration,
                completesInitialSizing));
    }

    /// <summary>[Server, game thread] Record the current host's live peer for this battle, from one of its
    /// own requests. A non-host requester leaves the record untouched.</summary>
    private void RememberHostPeer(string mapEventId, string requesterId, NetPeer requester)
    {
        if (requester == null) return;
        if (!hostRegistry.TryGet(mapEventId, out var assignment) || assignment.HostControllerId != requesterId)
            return;

        hostPeers[mapEventId] = new HostEndpoint { ControllerId = requesterId, Peer = requester };
    }

    private void RememberReservePeer(string mapEventId, string requesterId, NetPeer requester)
    {
        if (requester == null || string.IsNullOrEmpty(requesterId)) return;
        if (!reservePeers.TryGetValue(mapEventId, out var peers))
        {
            peers = new Dictionary<string, NetPeer>();
            reservePeers[mapEventId] = peers;
        }
        peers[requesterId] = requester;
    }

    /// <summary>[Server] The builder allocated a party after the frozen plan. Re-send each active authority's
    /// complete scope after the add broadcast, so the reliable-ordered stream delivers identity before reserve.</summary>
    private void Handle_BattleReserveScopeChanged(MessagePayload<BattleReserveScopeChanged> payload)
    {
        if (ModInformation.IsClient) return;

        var mapEventId = payload.What.MapEventId;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MapEvent>(mapEventId, out var mapEvent))
                return;

            int transferred = DrainRetainedPrioritySlots(mapEventId, mapEvent);
            int refreshed = 0;
            if (transferred == 0)
            {
                refreshed = RefreshReserveAuthorities(mapEventId, mapEvent);
                BroadcastPrioritySlotSnapshot(
                    mapEventId,
                    reserveBuilder.GetPrioritySlotSnapshot(mapEvent));
            }

            Logger.Information("[BattleHost] Refreshed {Count} reserve authority/authorities and transferred {Transfers} retained slot(s) after battle {MapEventId} gained a post-plan party",
                refreshed, transferred, mapEventId);
        });
    }

    private void BroadcastPrioritySlotSnapshot(
        string mapEventId,
        IReadOnlyList<BattlePrioritySlotState> prioritySnapshot)
    {
        if (prioritySnapshot == null) return;

        foreach (var state in prioritySnapshot)
        {
            if (state.TransferId <= 0)
            {
                network.SendAll(new NetworkBattlePriorityWaitQueued(
                    mapEventId,
                    state.WaitingPartyId,
                    resetExistingState: false));
                continue;
            }

            network.SendAll(new NetworkBattlePrioritySlotAssigned(
                mapEventId,
                state.TransferId,
                state.WaitingPartyId,
                state.DonorPartyId));
            if (state.IsConsumed)
            {
                network.SendAll(new NetworkBattlePrioritySlotConsumed(
                    mapEventId,
                    state.TransferId,
                    state.WaitingPartyId));
            }
        }
    }

    /// <summary>[Server] The waiting player's custom spawn consumed its transfer. Completing it in the frozen
    /// plan prevents a later ordinary disconnect from restoring a slot that is already on the field.</summary>
    private void Handle_NetworkBattlePrioritySlotConsumed(
        MessagePayload<NetworkBattlePrioritySlotConsumed> payload)
    {
        if (ModInformation.IsClient) return;

        var message = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MapEvent>(message.MapEventId, out var mapEvent))
                return;
            if (!reserveBuilder.CompletePrioritySpawn(
                    mapEvent,
                    message.TransferId,
                    message.WaitingPartyId))
            {
                return;
            }

            // Peers may not have received the P2P puppet yet. Mark the transfer consumed everywhere without
            // clearing its cap gate; puppet registration or a later consumed-party departure completes it.
            network.SendAll(new NetworkBattlePrioritySlotConsumed(
                message.MapEventId,
                message.TransferId,
                message.WaitingPartyId));
            Logger.Information("[BattleHost] Priority slot {TransferId} in {MapEventId} was consumed by {WaitingParty}",
                message.TransferId, message.MapEventId, message.WaitingPartyId);
        });
    }

    /// <summary>[Server] The waiting client cannot field its assigned hero. Validate the exact active transfer,
    /// then move it to the next waiter or restore its donor.</summary>
    private void Handle_NetworkBattlePrioritySlotDeclined(
        MessagePayload<NetworkBattlePrioritySlotDeclined> payload)
    {
        if (ModInformation.IsClient) return;

        var message = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MapEvent>(message.MapEventId, out var mapEvent))
                return;
            if (!reserveBuilder.TryDeclinePrioritySlot(
                    mapEvent,
                    message.TransferId,
                    message.WaitingPartyId,
                    out var transfer,
                    out var released))
            {
                return;
            }

            PublishPrioritySlotCleanup(
                message.MapEventId,
                mapEvent,
                transfer,
                released,
                "the waiting player declined it");
        });
    }

    /// <summary>[Server] Transfer one departed human's frozen slot to the first waiting player. Every active
    /// authority receives the changed reserve plan before the assignment marker, so each peer can stop the
    /// donor's replacement and let the waiting player enter on the same reliable-ordered stream.</summary>
    private void Handle_BattleHumanSlotFreed(MessagePayload<BattleHumanSlotFreed> payload)
    {
        if (ModInformation.IsClient) return;

        var mapEventId = payload.What.MapEventId;
        var donorPartyId = payload.What.PartyId;
        int troopSeed = payload.What.TroopSeed;
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MapEvent>(mapEventId, out var mapEvent))
                return;
            if (!TryRememberPrioritySlotDeparture(mapEventId, donorPartyId, troopSeed))
                return;
            if (!reserveBuilder.TryTransferInitialSpawnOnDeparture(
                    mapEvent,
                    donorPartyId,
                    out var transfer,
                    out var settledTransfer))
                return;

            PublishPrioritySlotTransfer(
                mapEventId,
                mapEvent,
                transfer,
                settledTransfer,
                "a human slot became available");
        });
    }

    private int DrainRetainedPrioritySlots(string mapEventId, MapEvent mapEvent)
    {
        int transferred = 0;
        while (reserveBuilder.TryTransferRetainedInitialSpawn(
                   mapEvent,
                   out var transfer,
                   out var settledTransfer))
        {
            PublishPrioritySlotTransfer(
                mapEventId,
                mapEvent,
                transfer,
                settledTransfer,
                "a retained human slot was claimed");
            transferred++;
        }

        return transferred;
    }

    private void PublishPrioritySlotTransfer(
        string mapEventId,
        MapEvent mapEvent,
        BattleInitialSpawnTransfer transfer,
        BattleInitialSpawnTransfer settledTransfer,
        string reason)
    {
        if (settledTransfer.TransferId > 0)
        {
            network.SendAll(new NetworkBattlePrioritySlotSettled(
                mapEventId,
                settledTransfer.TransferId,
                settledTransfer.WaitingPartyId));
        }
        if (transfer.TransferId <= 0)
        {
            Logger.Information("[BattleHost] Settled priority slot {TransferId} in {MapEventId} after {WaitingParty}'s human departed; its native replacement retains the slot",
                settledTransfer.TransferId,
                mapEventId,
                settledTransfer.WaitingPartyId);
            return;
        }

        int refreshed = RefreshReserveAuthorities(mapEventId, mapEvent);
        network.SendAll(new NetworkBattlePrioritySlotAssigned(
            mapEventId,
            transfer.TransferId,
            transfer.WaitingPartyId,
            transfer.DonorPartyId));

        Logger.Information("[BattleHost] Assigned priority slot {TransferId} in {MapEventId}: {DonorParty} -> {WaitingParty} because {Reason}; refreshed {Count} reserve authority/authorities first",
            transfer.TransferId,
            mapEventId,
            transfer.DonorPartyId,
            transfer.WaitingPartyId,
            reason,
            refreshed);
    }

    private bool TryRememberPrioritySlotDeparture(string mapEventId, string partyId, int troopSeed)
    {
        if (!prioritySlotDepartures.TryGetValue(mapEventId, out var parties))
        {
            parties = new Dictionary<string, HashSet<int>>();
            prioritySlotDepartures[mapEventId] = parties;
        }
        if (!parties.TryGetValue(partyId, out var seeds))
        {
            seeds = new HashSet<int>();
            parties[partyId] = seeds;
        }
        return seeds.Add(troopSeed);
    }

    /// <summary>[Server] A connected campaign party is leaving before mission membership necessarily reports
    /// it. Release any unconsumed wait while its MapEventParty is still addressable.</summary>
    private void Handle_BattlePartyLeaving(MessagePayload<BattlePartyLeaving> payload)
    {
        if (ModInformation.IsClient) return;

        var message = payload.What;
        GameThread.RunSafe(() =>
        {
            ClearPrioritySlotDepartures(message.MapEventId, message.MapEventPartyId);
            if (!objectManager.TryGetObjectWithLogging<MapEvent>(message.MapEventId, out var mapEvent))
                return;

            bool isDirectPlayerParty = IsDirectPlayerMapEventParty(
                mapEvent,
                message.MapEventPartyId);
            if (reserveBuilder.TryReassignOrReleasePrioritySlot(
                    mapEvent,
                    message.MapEventPartyId,
                    out var transfer,
                    out var released))
            {
                PublishPrioritySlotCleanup(
                    message.MapEventId,
                    mapEvent,
                    transfer,
                    released,
                    "the waiting party left the campaign battle");
            }

            if (!isDirectPlayerParty)
                return;

            int retained = reserveBuilder.RetainInitialSpawnVacancies(
                mapEvent,
                message.MapEventPartyId);
            int transferred = DrainRetainedPrioritySlots(message.MapEventId, mapEvent);
            Logger.Information("[BattleHost] Retained {Retained} initial slot(s) and transferred {Transferred} after player party {PartyId} left battle {MapEventId}",
                retained, transferred, message.MapEventPartyId, message.MapEventId);
        });
    }

    private bool IsDirectPlayerMapEventParty(MapEvent mapEvent, string mapEventPartyId)
    {
        foreach (var party in mapEvent.AttackerSide.Parties)
        {
            if (objectManager.TryGetId(party, out var partyId)
                && partyId == mapEventPartyId)
            {
                return playerManager.Contains(party.Party?.MobileParty);
            }
        }
        foreach (var party in mapEvent.DefenderSide.Parties)
        {
            if (objectManager.TryGetId(party, out var partyId)
                && partyId == mapEventPartyId)
            {
                return playerManager.Contains(party.Party?.MobileParty);
            }
        }

        return false;
    }

    // [Server, game thread] Push the current complete reserve scope to each connected participant known to
    // this battle. A loading client still receives only its nonempty side until its mission-ready role is final.
    private int RefreshReserveAuthorities(string mapEventId, MapEvent mapEvent)
    {
        if (!reservePeers.TryGetValue(mapEventId, out var peers))
            return 0;

        int refreshed = 0;
        foreach (var endpoint in new List<KeyValuePair<string, NetPeer>>(peers))
        {
            if (endpoint.Value == null || endpoint.Value.ConnectionState != ConnectionState.Connected)
                continue;
            if (!IsRequesterInBattle(mapEvent, endpoint.Key) || HasPendingReturn(mapEventId, endpoint.Key))
                continue;

            // Explicit empty sides finalize sizing only after this authority has a role in the host generation.
            bool includeEmptySides = IsMissionReadyAuthority(mapEventId, endpoint.Key);
            SendOwnedReserves(mapEventId, mapEvent, endpoint.Value, endpoint.Key, includeEmptySides);
            refreshed++;
        }
        return refreshed;
    }

    private bool IsMissionReadyAuthority(string mapEventId, string controllerId)
    {
        if (!hostRegistry.TryGet(mapEventId, out var assignment))
            return false;
        if (assignment.HostControllerId == controllerId)
            return true;
        foreach (var successorId in assignment.SuccessorControllerIds)
            if (successorId == controllerId)
                return true;
        return false;
    }

    private bool HasPendingReturn(string mapEventId, string controllerId)
    {
        if (!pendingReturns.TryGetValue(mapEventId, out var pendings)) return false;
        foreach (var pending in pendings)
            if (pending.ReturnerControllerId == controllerId)
                return true;
        return false;
    }

    /// <summary>
    /// [Server, game thread] The requester is back inside a battle it had DROPPED from: its parties leave the
    /// host's reserve scope (BR-033). Because nothing else tells the holder to let go, the current host is
    /// RE-FED its owned set — now without the returned parties, with explicit empties so a side that became
    /// empty is cleared — using the same NetworkBattleTroopReserve REPLACE semantics migration re-feeds use.
    /// The refresh is sent with <c>FlushRequested</c>: the ledger only holds the host's last THROTTLED
    /// progress report and can lag its true local pointer, so the returner's grant is DEFERRED (a pending
    /// return is recorded; returns true) until the host's flush acks land the dropped parties' final
    /// pointers in the ledger — otherwise descriptors the host fielded in the report gap would be re-served
    /// and spawn twice. Fallbacks guarantee the returner is never stranded: no live host peer serves it
    /// immediately from the ledger (return false — the pre-handshake race accepted), the host's departure
    /// serves outstanding pendings, and the campaign-tick deadline serves them when no ack ever arrives.
    /// Ordering guarantees: all scope decisions (drop-marking, this return, both replies) serialize on the
    /// server game thread against one ledger snapshot; server→host messages are reliable-ordered on the
    /// host's stream, so this refresh can never be overtaken by an earlier, fatter grant; and pointers can
    /// never rewind — the refresh carries the ledger's monotonic pointers and the supplier resumes from
    /// max(local, server). A no-op for members that never dropped, so a duplicate request changes nothing.
    /// </summary>
    /// <returns>True when the requester's grant is handled through a pending return (deferred behind the
    /// host's flush acks — possibly already completed inline) so the caller must not also serve it.</returns>
    private bool HandleControllerReturn(string mapEventId, MapEvent mapEvent, string requesterId, NetPeer requesterPeer, bool includeEmptySides)
    {
        if (string.IsNullOrEmpty(requesterId)) return false;
        if (!absentControllers.TryGetValue(mapEventId, out var absent) || !absent.Remove(requesterId))
            return false;
        if (absent.Count == 0)
            absentControllers.Remove(mapEventId);

        Logger.Information("[BattleHost] {Controller} returned to battle {MapEventId}; its parties leave the host's reserve scope",
            requesterId, mapEventId);

        if (!hostRegistry.TryGet(mapEventId, out var assignment))
            return false;
        if (assignment.HostControllerId == requesterId)
            return false; // the returner IS the current host — the grant it is about to receive is the refresh

        if (!hostPeers.TryGetValue(mapEventId, out var host) || host.ControllerId != assignment.HostControllerId)
        {
            // A promoted host that has not re-requested yet has no live peer recorded — and needs no refresh:
            // its own upcoming adoption re-request is already served the post-return scope. With no holder to
            // flush, the caller serves the returner straight from the ledger (fallback: never strand it).
            Logger.Information("[BattleHost] No live peer recorded for host {Host} of {MapEventId}; skipping reserve refresh (its next request is already re-scoped)",
                assignment.HostControllerId, mapEventId);
            return false;
        }

        // Build the shrunk refresh BEFORE recording the pending so the awaited ack count (one per side
        // message) is known, and record the pending BEFORE sending so an ack can never miss it — the sends
        // can complete the whole handshake inline (the in-process test harness delivers synchronously).
        var refresh = BuildOwnedReserves(mapEventId, mapEvent, host.ControllerId, includeEmptySides: true);
        if (refresh.Count == 0 || requesterPeer == null)
        {
            // Nothing to flush, or no peer to defer a grant for: refresh without a handshake and let the
            // caller serve immediately from the ledger.
            SendSideReserves(
                host.Peer,
                mapEventId,
                refresh,
                flushRequested: false,
                completesInitialSizing: true);
            Logger.Information("[BattleHost] Refreshed host {Host}'s reserve scope for {MapEventId} after {Controller} returned",
                host.ControllerId, mapEventId, requesterId);
            return false;
        }

        long grantGeneration = ++nextReserveGrantGeneration;
        var pending = new PendingReturn
        {
            MapEventId = mapEventId,
            ReturnerControllerId = requesterId,
            ReturnerPeer = requesterPeer,
            HostControllerId = host.ControllerId,
            IncludeEmptySides = includeEmptySides,
            AwaitedAcks = refresh.Count,
            GrantGeneration = grantGeneration,
            DeadlineUtc = DateTime.UtcNow + FlushAckDeadline,
        };
        if (!pendingReturns.TryGetValue(mapEventId, out var pendings))
        {
            pendings = new List<PendingReturn>();
            pendingReturns[mapEventId] = pendings;
        }
        pendings.Add(pending);

        SendSideReserves(
            host.Peer,
            mapEventId,
            refresh,
            flushRequested: true,
            completesInitialSizing: true,
            grantGeneration: grantGeneration);
        Logger.Information("[BattleHost] Refreshed host {Host}'s reserve scope for {MapEventId} after {Controller} returned; the return grant awaits {Acks} flush ack(s)",
            host.ControllerId, mapEventId, requesterId, refresh.Count);
        return true;
    }

    /// <summary>[Server, game thread] A repeat request from a controller whose return grant is still
    /// PENDING — e.g. the mission-ready election request arriving while the entry request's flush handshake
    /// is in flight: fold it into the pending grant instead of serving early from the not-yet-caught-up
    /// ledger. The eventual grant is upgraded to explicit empties when any folded request needed them (the
    /// election reply's sizing contract), and the peer is refreshed. Duplicate returns are thereby
    /// idempotent: one grant serves them all once the acks (or a fallback) land.</summary>
    private bool TryDeferToPendingReturn(string mapEventId, string requesterId, NetPeer requesterPeer, bool includeEmptySides)
    {
        if (!pendingReturns.TryGetValue(mapEventId, out var pendings))
            return false;

        foreach (var pending in pendings)
        {
            if (pending.ReturnerControllerId != requesterId)
                continue;

            pending.IncludeEmptySides |= includeEmptySides;
            if (requesterPeer != null)
                pending.ReturnerPeer = requesterPeer;
            Logger.Information("[BattleHost] Folded {Controller}'s reserve request for {MapEventId} into its pending return (grant deferred until the host's flush ack)",
                requesterId, mapEventId);
            return true;
        }
        return false;
    }

    /// <summary>[Server] A FLUSH ACK from the shrunk holder (BR-033 handshake): land the flushed pointers in
    /// the LEDGER first, then complete the matching pending return once all of its refresh messages
    /// are acked — the returner's grant is then computed at the caught-up pointers. The pointers are applied
    /// to the injected ledger directly rather than relying on <c>BattleSupplyProgressHandler</c> having run
    /// first: broker delivery order between two subscribers is registration-order dependent with no
    /// contract, while the direct apply makes ack-before-grant self-contained — and the double application
    /// is harmless because <c>ReportSupplied</c> is monotonic and clamped. The echoed grant generation keeps
    /// a delayed ack from a fallback-completed return from completing a newer return. A stale or duplicate ack
    /// still lands its pointers and changes nothing else. Periodic (non-flush) reports stay
    /// <c>BattleSupplyProgressHandler</c>'s job.</summary>
    private void Handle_NetworkBattleSupplyProgress(MessagePayload<NetworkBattleSupplyProgress> payload)
    {
        if (ModInformation.IsClient) return;

        var message = payload.What;
        if (!message.IsFlush) return;

        // Pending-return state is game-thread-only (as is serving the grant).
        GameThread.RunSafe(() =>
        {
            if (message.Entries != null)
                foreach (var entry in message.Entries)
                    ledger.ReportSupplied(message.MapEventId, entry.PartyId, entry.SuppliedCount);

            if (!pendingReturns.TryGetValue(message.MapEventId, out var pendings) || pendings.Count == 0)
                return;

            PendingReturn pending = null;
            foreach (var candidate in pendings)
            {
                if (candidate.GrantGeneration != message.GrantGeneration) continue;
                pending = candidate;
                break;
            }
            if (pending == null)
                return;

            if (--pending.AwaitedAcks > 0)
                return;

            pendings.Remove(pending);
            if (pendings.Count == 0)
                pendingReturns.Remove(message.MapEventId);

            ServePendingReturn(pending, "the host's flush acks caught the ledger up");
        });
    }

    /// <summary>[Server, game thread — published by the Campaign.Tick postfix] Deadline sweep for pending
    /// returns: an interrupted flush must not strand the returner — past <see cref="FlushAckDeadline"/> it
    /// is served from the current ledger
    /// with a warning, accepting the pre-handshake race for that one grant. Tick-driven on purpose: no
    /// background timers into the broker/network.</summary>
    private void Handle_CampaignTick(MessagePayload<CampaignTick> payload)
    {
        if (ModInformation.IsClient) return;
        if (pendingReturns.Count == 0) return;

        var now = DateTime.UtcNow;
        List<PendingReturn> expired = null;
        foreach (var pendings in pendingReturns.Values)
            foreach (var pending in pendings)
                if (now >= pending.DeadlineUtc)
                    (expired ??= new List<PendingReturn>()).Add(pending);
        if (expired == null) return;

        foreach (var pending in expired)
        {
            if (pendingReturns.TryGetValue(pending.MapEventId, out var pendings))
            {
                pendings.Remove(pending);
                if (pendings.Count == 0)
                    pendingReturns.Remove(pending.MapEventId);
            }

            Logger.Warning("[BattleHost] Host {Host} never acked the reserve flush for {MapEventId} within {Deadline}; serving {Controller}'s return grant from the current ledger",
                pending.HostControllerId, pending.MapEventId, FlushAckDeadline, pending.ReturnerControllerId);
            ServePendingReturn(pending, "flush-ack deadline");
        }
    }

    // [Server, game thread] Complete a pending return: serve the returner its owned reserves at the
    // ledger's current pointers (isHost/absent are evaluated at serve time, so a returner promoted while it
    // waited is served its post-promotion scope). The reason makes the completing path visible in the logs.
    private void ServePendingReturn(PendingReturn pending, string reason)
    {
        if (!objectManager.TryGetObject<MapEvent>(pending.MapEventId, out var mapEvent))
        {
            Logger.Warning("[BattleHost] Dropping the pending return grant of {Controller} to {MapEventId}: the map event no longer resolves",
                pending.ReturnerControllerId, pending.MapEventId);
            return;
        }

        SendEntryReserves(pending.MapEventId, mapEvent, pending.ReturnerPeer, pending.ReturnerControllerId, pending.IncludeEmptySides);
        Logger.Information("[BattleHost] Served {Controller}'s deferred return grant for {MapEventId} ({Reason})",
            pending.ReturnerControllerId, pending.MapEventId, reason);
    }

    // [Server, game thread] The host owing flush acks DEPARTED: no ack is coming — serve every pending
    // return that was waiting on it from the CURRENT ledger (whatever the departed host fielded past its
    // last throttled report is lost with it; the pre-handshake race is accepted over stranding a returner).
    private void ServePendingReturnsAwaitingHost(string mapEventId, string departedHostId)
    {
        if (!pendingReturns.TryGetValue(mapEventId, out var pendings))
            return;

        var toServe = new List<PendingReturn>();
        for (int i = pendings.Count - 1; i >= 0; i--)
        {
            if (pendings[i].HostControllerId != departedHostId)
                continue;
            toServe.Insert(0, pendings[i]);
            pendings.RemoveAt(i);
        }
        if (pendings.Count == 0)
            pendingReturns.Remove(mapEventId);

        foreach (var pending in toServe)
            ServePendingReturn(pending, "the host departed before its flush ack");
    }

    // [Server, game thread] The RETURNER dropped again while its grant was still pending: cancel it — there
    // is no one to serve, and its next return restarts the handshake against the then-current scope.
    private void CancelPendingReturns(string mapEventId, string returnerControllerId)
    {
        if (!pendingReturns.TryGetValue(mapEventId, out var pendings))
            return;

        for (int i = pendings.Count - 1; i >= 0; i--)
        {
            if (pendings[i].ReturnerControllerId != returnerControllerId)
                continue;
            pendings.RemoveAt(i);
            Logger.Information("[BattleHost] Cancelled the pending return grant of {Controller} to {MapEventId}: it departed again before the flush ack",
                returnerControllerId, mapEventId);
        }
        if (pendings.Count == 0)
            pendingReturns.Remove(mapEventId);
    }

    /// <summary>[Server] A member departed a battle: promote the next successor if it was the host (host
    /// migration), or drop it from the successor line otherwise; broadcast the updated assignment.</summary>
    private void Handle_MissionMemberDeparted(MessagePayload<MissionMemberDeparted> payload)
    {
        if (ModInformation.IsClient) return;

        var controllerId = payload.What.ControllerId;
        var mapEventId = payload.What.InstanceId;

        // Release before a later battle-start request can be handled on this poll thread. The reliable ordered
        // stream guarantees the departure is published first, but the host/reserve cleanup below runs next frame.
        if (payload.What.IsInstanceEmpty && ServerBattleModeArbiter.ReleaseMission(mapEventId))
            network.SendAll(new NetworkBattleModeSet(mapEventId, (int)BattleStartMode.Unclaimed));

        // Mutates the shared assignment, so run on the main thread (serializes with election).
        GameThread.RunSafe(() =>
        {
            if (reservePeers.TryGetValue(mapEventId, out var peers))
            {
                peers.Remove(controllerId);
                if (peers.Count == 0)
                    reservePeers.Remove(mapEventId);
            }

            MapEvent liveMapEvent = null;
            string withdrawingPartyId = null;
            if (!payload.What.IsInstanceEmpty
                && objectManager.TryGetObject<MapEvent>(mapEventId, out liveMapEvent))
            {
                if (payload.What.WasRetreat
                    && TryGetControllerMapEventParty(liveMapEvent, controllerId, out var withdrawingParty))
                {
                    objectManager.TryGetId(withdrawingParty, out withdrawingPartyId);
                }

                HandleDepartedPriorityPlayer(mapEventId, liveMapEvent, controllerId);
            }

            // BR-017: no players remain in the mission instance — destroy the battle instance record
            // outright, regardless of whether the departed controller was the recorded host. Keying on the
            // server-observed empty instance (not on who left last) also covers a successor emptying the
            // instance while the host's own departure was never observed: an out-of-date successor line must
            // not leave the assignment — or a promotion to an already-departed player — behind. The map
            // event itself PERSISTS at its last synchronized state; the mode release above reopens both
            // resolution options for it (a new mission per BR-002/BR-054, or player simulation — BR-003's
            // exclusion resets with the instance). Idempotent: a duplicate empty departure finds nothing.
            if (payload.What.IsInstanceEmpty)
            {
                if (hostRegistry.TryGet(mapEventId, out _))
                {
                    Logger.Information("[BattleHost] Battle {MapEventId} instance is empty after {Controller} departed; clearing host assignment",
                        mapEventId, controllerId);
                    hostRegistry.Remove(mapEventId);
                }

                // Forget the WHOLE map event's reserves — not just the leaver's own party — so restarting the
                // SAME map event re-flattens every party (the AI/enemy parties the host had been fielding
                // included). Otherwise their supplied pointers stay at the end and only the re-flattened
                // rejoiner's party re-spawns.
                if (objectManager.TryGetObject<MapEvent>(mapEventId, out var abandonedEvent))
                    reserveBuilder.ForgetMapEvent(abandonedEvent);

                // Battle over as far as this instance is concerned — drop its scope bookkeeping with it.
                ClearBattleRecords(mapEventId);
                return;
            }

            // Departure bookkeeping runs BEFORE the host-assignment check: a member can leave while every
            // participant is still on the loading screen, so no host has been elected yet. A withdrawal
            // forgets the player's reserve party, so a rejoin re-flattens and re-spawns it fresh. An explicit
            // non-withdrawing absence keeps the reserve and marks the member ABSENT so its parties resolve into
            // the reserve scope of whoever fields them next. Gating this on
            // an existing host assignment orphaned a member that dropped BEFORE any election: the eventual
            // first-ready host's reserve build never saw the drop, so it never inherited that still-registered
            // party.
            if (payload.What.WasRetreat)
            {
                if (liveMapEvent != null)
                {
                    ClearPrioritySlotDeparturesForController(mapEventId, liveMapEvent, controllerId);
                    int retained = withdrawingPartyId == null
                        ? 0
                        : reserveBuilder.RetainInitialSpawnVacancies(
                            liveMapEvent,
                            withdrawingPartyId);
                    int transferred = DrainRetainedPrioritySlots(mapEventId, liveMapEvent);
                    reserveBuilder.ForgetController(liveMapEvent, controllerId);
                    Logger.Information("[BattleHost] Retained {Retained} initial slot(s) and transferred {Transferred} after {Controller} withdrew from {MapEventId}",
                        retained, transferred, controllerId, mapEventId);
                }
            }
            else
            {
                MarkAbsent(mapEventId, controllerId);
            }

            // If the departed member had a return grant pending (it re-entered and dropped AGAIN before the
            // host's flush ack), the grant is moot — cancel it rather than serve a gone peer.
            CancelPendingReturns(mapEventId, controllerId);

            if (!hostRegistry.TryGet(mapEventId, out var assignment))
                return; // no host elected yet — the departure bookkeeping above is all that applies

            var successors = new List<string>(assignment.SuccessorControllerIds);

            if (assignment.HostControllerId == controllerId)
            {
                if (successors.Count == 0)
                {
                    // The recorded host left and no mission-ready successor exists — but the instance is NOT
                    // empty here (the empty branch above already returned), so a participant is still LOADING
                    // and will become the eventual host. Remove the now-hostless assignment and the departed
                    // host's stale peer, and re-flatten the reserves: with no surviving ready client, none of
                    // the departed host's on-field troops persist anywhere, so the eventual host must re-spawn
                    // the whole (casualty-reduced) roster from a reset pointer. Preserve the frozen spawn plan
                    // because this mission instance is still live, and do NOT clear the scope bookkeeping:
                    // the departed host stays ABSENT so its party falls to the eventual host's reserve scope.
                    // Clearing it here (as the abandonment teardown does) let the party resolve back to the
                    // still-registered departed host, so the eventual host never received it. The full teardown
                    // — which also clears the absent markers — runs only when the instance is observed EMPTY.
                    Logger.Warning("[BattleHost] Host {Host} left battle {MapEventId} with no ready successors (players still loading); re-flattening reserves but keeping the departed host absent so the eventual host inherits its party",
                        controllerId, mapEventId);
                    hostRegistry.Remove(mapEventId);
                    hostPeers.Remove(mapEventId); // the departed host's recorded peer is stale

                    if (objectManager.TryGetObject<MapEvent>(mapEventId, out var liveEvent))
                        reserveBuilder.RebuildMapEventReserves(liveEvent);

                    // A returner whose grant was pending on the departed host's flush ack must still be
                    // served (it is one of the still-loading participants). AFTER the re-flatten above, so
                    // it resumes from the same reset pointers the eventual host will re-spawn from.
                    ServePendingReturnsAwaitingHost(mapEventId, controllerId);
                    return;
                }

                // The departed host's recorded peer is stale; the promoted host's own adoption re-request
                // (its sweep) re-captures the live one. Validated-at-use anyway — this is hygiene.
                hostPeers.Remove(mapEventId);

                // Promote the earliest-joined successor still present (the line is kept current as members leave).
                // BR-102: the host CHANGED, so the promotion opens the next hosting generation (epoch + 1).
                var newHost = successors[0];
                successors.RemoveAt(0);
                var promoted = new BattleHostAssignment(newHost, successors, NextEpoch(mapEventId, assignment.Epoch));
                hostRegistry.Set(mapEventId, promoted);

                Logger.Information("[BattleHost] Host {Old} left battle {MapEventId}; promoted {New} at epoch {Epoch} (successors: {Successors})",
                    controllerId, mapEventId, newHost, promoted.Epoch, string.Join(", ", successors));

                network.SendAll(ToMessage(mapEventId, promoted));

                // The departed host can no longer ack a reserve flush: serve any return grant that was
                // pending on it from the current ledger. AFTER the promotion, so the grant is scoped
                // against the new assignment (a promoted returner is served its host scope).
                ServePendingReturnsAwaitingHost(mapEventId, controllerId);
            }
            else if (successors.Remove(controllerId))
            {
                // Successor-line cleanup: the host did not change, so the epoch is unchanged (BR-102).
                var updated = new BattleHostAssignment(assignment.HostControllerId, successors, assignment.Epoch);
                hostRegistry.Set(mapEventId, updated);

                Logger.Information("[BattleHost] Successor {Controller} left battle {MapEventId}; successor line now: {Successors}",
                    controllerId, mapEventId, string.Join(", ", successors));

                network.SendAll(ToMessage(mapEventId, updated));
            }
        });
    }

    /// <summary>[Server] A loading player can disconnect before mission membership exists, so no
    /// <see cref="MissionMemberDeparted"/> is raised. The campaign disconnect path retains its controller id
    /// long enough to cancel or reassign a priority wait even before its first reserve request.</summary>
    private void Handle_BattlePlayerDisconnected(MessagePayload<BattlePlayerDisconnected> payload)
    {
        if (ModInformation.IsClient) return;

        var message = payload.What;
        GameThread.RunSafe(() =>
        {
            RemoveReservePeer(message.MapEventId, message.ControllerId);
            if (!objectManager.TryGetObject<MapEvent>(message.MapEventId, out var mapEvent))
                return;

            ClearPrioritySlotDeparturesForController(
                message.MapEventId,
                mapEvent,
                message.ControllerId);
            HandleDepartedPriorityPlayer(message.MapEventId, mapEvent, message.ControllerId);
        });
    }

    private void RemoveReservePeer(string mapEventId, string controllerId)
    {
        if (!reservePeers.TryGetValue(mapEventId, out var peers))
            return;

        peers.Remove(controllerId);
        if (peers.Count == 0)
            reservePeers.Remove(mapEventId);
    }

    // [Server, game thread] An assigned player departed before acknowledging its spawn. Reassign the same
    // transfer id when another player is waiting, otherwise restore the original donor and cancel the gate.
    private void HandleDepartedPriorityPlayer(string mapEventId, MapEvent mapEvent, string controllerId)
    {
        if (!reserveBuilder.TryReassignOrReleasePrioritySlotForController(
                mapEvent,
                controllerId,
                out var transfer,
                out var released))
        {
            return;
        }

        PublishPrioritySlotCleanup(
            mapEventId,
            mapEvent,
            transfer,
            released,
            "the waiting player departed");
    }

    private void PublishPrioritySlotCleanup(
        string mapEventId,
        MapEvent mapEvent,
        BattleInitialSpawnTransfer transfer,
        bool released,
        string reason)
    {
        int refreshed = RefreshReserveAuthorities(mapEventId, mapEvent);
        if (released)
        {
            network.SendAll(new NetworkBattlePrioritySlotCancelled(
                mapEventId,
                transfer.TransferId,
                transfer.WaitingPartyId,
                transfer.DonorPartyId));
            Logger.Information("[BattleHost] Cancelled unconsumed priority slot {TransferId} in {MapEventId} because {Reason}; waiting={WaitingParty}, restored donor={DonorParty}, refreshed {Count} reserve authority/authorities first",
                transfer.TransferId, mapEventId, reason, transfer.WaitingPartyId, transfer.DonorPartyId, refreshed);
            return;
        }

        network.SendAll(new NetworkBattlePrioritySlotAssigned(
            mapEventId,
            transfer.TransferId,
            transfer.WaitingPartyId,
            transfer.DonorPartyId));
        Logger.Information("[BattleHost] Reassigned unconsumed priority slot {TransferId} in {MapEventId} to {WaitingParty} because {Reason}; refreshed {Count} reserve authority/authorities first",
            transfer.TransferId, mapEventId, transfer.WaitingPartyId, reason, refreshed);
    }

    private void ClearPrioritySlotDeparturesForController(
        string mapEventId,
        MapEvent mapEvent,
        string controllerId)
    {
        if (!TryGetControllerMapEventParty(mapEvent, controllerId, out var mapEventParty)
            || !objectManager.TryGetId(mapEventParty, out var partyId))
        {
            return;
        }

        ClearPrioritySlotDepartures(mapEventId, partyId);
    }

    private void ClearPrioritySlotDepartures(string mapEventId, string partyId)
    {
        if (!prioritySlotDepartures.TryGetValue(mapEventId, out var parties))
            return;

        parties.Remove(partyId);
        if (parties.Count == 0)
            prioritySlotDepartures.Remove(mapEventId);
    }

    /// <summary>[Client] Store the server's host assignment for this battle.</summary>
    private void Handle_NetworkBattleHostAssigned(MessagePayload<NetworkBattleHostAssigned> payload)
    {
        if (ModInformation.IsServer) return;

        var message = payload.What;

        // Capture the host we knew before applying the update, so we can detect a migration TO us.
        string previousHost = null;
        if (hostRegistry.TryGet(message.MapEventId, out var previous))
        {
            // BR-102: assignments are ordered by their host epoch. One LOWER than what we already hold is a
            // stale/out-of-order broadcast (e.g. re-delivered around a migration) and must not overwrite the
            // newer assignment; an EQUAL epoch is a successor-line update for the same host and applies.
            if (message.Epoch < previous.Epoch)
            {
                Logger.Information("[BattleHost] Ignoring stale host assignment for {MapEventId}: epoch {Stale} < current {Current} (named {Host})",
                    message.MapEventId, message.Epoch, previous.Epoch, message.HostControllerId);
                return;
            }

            previousHost = previous.HostControllerId;
        }

        var assignment = new BattleHostAssignment(
            message.HostControllerId,
            message.SuccessorControllerIds ?? Array.Empty<string>(),
            message.Epoch);
        hostRegistry.Set(message.MapEventId, assignment);

        Logger.Information("[BattleHost] Battle {MapEventId} host is {Host}{IsMe} at epoch {Epoch} (successors: {Successors})",
            message.MapEventId,
            message.HostControllerId,
            hostRegistry.IsHost(message.MapEventId) ? " (this client)" : "",
            message.Epoch,
            string.Join(", ", assignment.SuccessorControllerIds));

        // Migration: the host changed and it is now us — adopt the previous host's orphaned agents so the
        // battle continues uninterrupted (the controller does the actual adoption with the live mission).
        if (previousHost != null
            && previousHost != message.HostControllerId
            && message.HostControllerId == controllerIdProvider.ControllerId)
        {
            Logger.Information("[BattleHost] Became host of {MapEventId} via migration from {Old}", message.MapEventId, previousHost);
            messageBroker.Publish(this, new BattleHostMigrated(message.MapEventId, previousHost));
        }
    }

    // True if the requesting controller's player party is in this map event. Defends against a stale or
    // cross-battle request electing a non-participant. Server-authoritative.
    private bool IsRequesterInBattle(MapEvent mapEvent, string requesterId)
    {
        foreach (var player in playerManager.Players)
        {
            if (player.ControllerId != requesterId)
                continue;
            return objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party)
                && party?.MapEvent == mapEvent;
        }
        return false;
    }

    // Append the requester to the successor line unless it is already the host or already queued. Returns the
    // new immutable assignment via <paramref name="updated"/>, or false when nothing changed.
    private static bool TryAppendSuccessor(BattleHostAssignment existing, string requesterId, out BattleHostAssignment updated)
    {
        updated = null;
        if (existing.HostControllerId == requesterId)
            return false;
        foreach (var successor in existing.SuccessorControllerIds)
            if (successor == requesterId)
                return false;

        // The host did not change, so the assignment stays in the same hosting generation (BR-102).
        var successors = new List<string>(existing.SuccessorControllerIds) { requesterId };
        updated = new BattleHostAssignment(existing.HostControllerId, successors, existing.Epoch);
        return true;
    }

    // [Server, game thread] A member was marked absent without withdrawing: its parties fall into the host's
    // reserve scope until it returns (see HandleControllerReturn).
    private void MarkAbsent(string mapEventId, string controllerId)
    {
        if (string.IsNullOrEmpty(controllerId)) return;

        if (!absentControllers.TryGetValue(mapEventId, out var absent))
        {
            absent = new HashSet<string>();
            absentControllers[mapEventId] = absent;
        }

        if (absent.Add(controllerId))
            Logger.Information("[BattleHost] {Controller} was marked absent from battle {MapEventId}; its parties fall to the host's reserve scope until it returns",
                controllerId, mapEventId);
    }

    // [Server, game thread] The battle instance ended (empty, or abandoned with no successors): drop its
    // per-battle scope bookkeeping alongside the assignment + reserve teardown. Pending returns die with
    // the instance too — an EMPTY instance means the pending returner itself is gone (nothing to serve; a
    // later re-entry restarts from the entry flow). The epoch watermark intentionally survives (see
    // issuedEpochs).
    private void ClearBattleRecords(string mapEventId)
    {
        absentControllers.Remove(mapEventId);
        hostPeers.Remove(mapEventId);
        reservePeers.Remove(mapEventId);
        pendingReturns.Remove(mapEventId);
        prioritySlotDepartures.Remove(mapEventId);
    }

    // [Server] Issue the battle's next host epoch (BR-102): one past the highest ever issued for this map
    // event (and past <paramref name="floor"/>, the current assignment's epoch when promoting — it may have
    // been seeded out-of-band), starting at 1. The watermark survives assignment removal so an
    // abandoned-and-re-entered battle cannot reuse an epoch (see the field's remarks).
    private int NextEpoch(string mapEventId, int floor = 0)
    {
        issuedEpochs.TryGetValue(mapEventId, out var last);
        var next = Math.Max(last, floor) + 1;
        issuedEpochs[mapEventId] = next;
        return next;
    }

    private static NetworkBattleHostAssigned ToMessage(string mapEventId, BattleHostAssignment assignment)
    {
        var successors = new string[assignment.SuccessorControllerIds.Count];
        for (int i = 0; i < successors.Length; i++)
            successors[i] = assignment.SuccessorControllerIds[i];

        return new NetworkBattleHostAssigned(mapEventId, assignment.HostControllerId, successors, assignment.Epoch);
    }
}
