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

    private sealed class HostEndpoint
    {
        public string ControllerId;
        public NetPeer Peer;
    }

    /// <summary>
    /// [Server] How long a deferred return grant waits for the host's flush ack (BR-033 handshake) before
    /// the returner is served from the current ledger anyway — the returner must NEVER be stranded on a
    /// lost ack or a legacy host that ignores <c>FlushRequested</c>. Checked on the campaign tick.
    /// Internal-settable so tests can drive the deadline without wall-clock waits.
    /// </summary>
    internal TimeSpan FlushAckDeadline { get; set; } = TimeSpan.FromSeconds(2.5);

    // [Server] Per battle: return grants DEFERRED behind the shrunk host's flush acks (BR-033 handshake).
    // The ledger only holds the host's last THROTTLED progress report, so the returner must not be served
    // until the host flushed its true local pointers for the parties the shrink refresh dropped. One entry
    // per outstanding returner, completed FIFO as the acks arrive (the refreshes travel one reliable-ordered
    // stream and every flagged side message produces exactly one ack, so ack order matches refresh order),
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
        messageBroker.Subscribe<NetworkRequestBattleReserves>(Handle_NetworkRequestBattleReserves);
        messageBroker.Subscribe<NetworkBattleSupplyProgress>(Handle_NetworkBattleSupplyProgress);
        messageBroker.Subscribe<CampaignTick>(Handle_CampaignTick);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerEnteredBattle>(Handle_PlayerEnteredBattle);
        messageBroker.Unsubscribe<BattleMissionReady>(Handle_BattleMissionReady);
        messageBroker.Unsubscribe<NetworkRequestBattleHost>(Handle_NetworkRequestBattleHost);
        messageBroker.Unsubscribe<NetworkBattleHostAssigned>(Handle_NetworkBattleHostAssigned);
        messageBroker.Unsubscribe<MissionMemberDeparted>(Handle_MissionMemberDeparted);
        messageBroker.Unsubscribe<NetworkRequestBattleReserves>(Handle_NetworkRequestBattleReserves);
        messageBroker.Unsubscribe<NetworkBattleSupplyProgress>(Handle_NetworkBattleSupplyProgress);
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

            SendOwnedReserves(mapEventId, mapEvent, requester, requesterId, includeEmptySides);
        });
    }

    /// <summary>[Server] Send the requester every reserve it owns, one message per side. With
    /// <paramref name="includeEmptySides"/> a side the requester owns nothing on is sent as an explicit empty
    /// (election/migration replies — finalizes the side so sizing proceeds); without it, empty sides are
    /// skipped (entry replies — the unowned sides are not decided until the election).</summary>
    private void SendOwnedReserves(string mapEventId, MapEvent mapEvent, NetPeer requester, string requesterId, bool includeEmptySides)
    {
        if (requester == null) return;
        SendSideReserves(requester, mapEventId,
            BuildOwnedReserves(mapEventId, mapEvent, requesterId, includeEmptySides), flushRequested: false);
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

    private void SendSideReserves(NetPeer receiver, string mapEventId, List<SideReserve> reserves, bool flushRequested)
    {
        if (receiver == null) return;
        foreach (var sideReserve in reserves)
            network.Send(receiver, new NetworkBattleTroopReserve(
                mapEventId, (int)sideReserve.Side, sideReserve.Parties, flushRequested));
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
            // Nothing to flush, or no peer to defer a grant for: refresh unflagged (legacy semantics) and
            // let the caller serve immediately from the ledger.
            SendSideReserves(host.Peer, mapEventId, refresh, flushRequested: false);
            Logger.Information("[BattleHost] Refreshed host {Host}'s reserve scope for {MapEventId} after {Controller} returned",
                host.ControllerId, mapEventId, requesterId);
            return false;
        }

        var pending = new PendingReturn
        {
            MapEventId = mapEventId,
            ReturnerControllerId = requesterId,
            ReturnerPeer = requesterPeer,
            HostControllerId = host.ControllerId,
            IncludeEmptySides = includeEmptySides,
            AwaitedAcks = refresh.Count,
            DeadlineUtc = DateTime.UtcNow + FlushAckDeadline,
        };
        if (!pendingReturns.TryGetValue(mapEventId, out var pendings))
        {
            pendings = new List<PendingReturn>();
            pendingReturns[mapEventId] = pendings;
        }
        pendings.Add(pending);

        SendSideReserves(host.Peer, mapEventId, refresh, flushRequested: true);
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
    /// the LEDGER first, then complete the battle's oldest pending return once all of its refresh messages
    /// are acked — the returner's grant is then computed at the caught-up pointers. The pointers are applied
    /// to the injected ledger directly rather than relying on <c>BattleSupplyProgressHandler</c> having run
    /// first: broker delivery order between two subscribers is registration-order dependent with no
    /// contract, while the direct apply makes ack-before-grant self-contained — and the double application
    /// is harmless because <c>ReportSupplied</c> is monotonic and clamped. Acks complete pendings FIFO per
    /// battle: the refreshes travel the host's one reliable-ordered stream and every flagged side message
    /// produces exactly one ack, so ack arrival order matches refresh order. A stale or duplicate ack (its
    /// pending already completed by a fallback, or none ever existed) still lands its pointers and changes
    /// nothing else. Periodic (non-flush) reports stay <c>BattleSupplyProgressHandler</c>'s job.</summary>
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

            var pending = pendings[0];
            if (--pending.AwaitedAcks > 0)
                return;

            pendings.RemoveAt(0);
            if (pendings.Count == 0)
                pendingReturns.Remove(message.MapEventId);

            ServePendingReturn(pending, "the host's flush acks caught the ledger up");
        });
    }

    /// <summary>[Server, game thread — published by the Campaign.Tick postfix] Deadline sweep for pending
    /// returns: a flush ack that never arrives (a legacy host without the handshake, a lost message) must
    /// not strand the returner — past <see cref="FlushAckDeadline"/> it is served from the current ledger
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

        SendOwnedReserves(pending.MapEventId, mapEvent, pending.ReturnerPeer, pending.ReturnerControllerId, pending.IncludeEmptySides);
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
                if (objectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent))
                    reserveBuilder.ForgetController(mapEvent, controllerId);
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
                    // host's stale peer, and re-flatten the reserves (ForgetMapEvent) exactly as the empty
                    // branch does: with no surviving ready client, none of the departed host's on-field troops
                    // persist anywhere, so the eventual host must re-spawn the whole (casualty-reduced) roster
                    // from a reset pointer — retaining the advanced pointer would under-spawn the troops that
                    // vanished with the host. But do NOT clear the scope bookkeeping (no ClearBattleRecords):
                    // the departed host stays ABSENT so its party falls to the eventual host's reserve scope.
                    // Clearing it here (as the abandonment teardown does) let the party resolve back to the
                    // still-registered departed host, so the eventual host never received it. The full teardown
                    // — which also clears the absent markers — runs only when the instance is observed EMPTY.
                    Logger.Warning("[BattleHost] Host {Host} left battle {MapEventId} with no ready successors (players still loading); re-flattening reserves but keeping the departed host absent so the eventual host inherits its party",
                        controllerId, mapEventId);
                    hostRegistry.Remove(mapEventId);
                    hostPeers.Remove(mapEventId); // the departed host's recorded peer is stale

                    if (objectManager.TryGetObject<MapEvent>(mapEventId, out var abandonedEvent))
                        reserveBuilder.ForgetMapEvent(abandonedEvent);

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
#if DEBUG
        if (RaidDebugFixture.TryGetMissionParticipant(mapEvent, requesterId, out _))
            return true;
#endif

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
        pendingReturns.Remove(mapEventId);
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
