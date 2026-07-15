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

    // [Server] Highest host epoch ever issued per battle instance (BR-102), retained across assignment
    // removal: clients keep their last assignment when a battle is fully abandoned (only the server's entry
    // is removed), so a re-election for the SAME map event must issue a HIGHER epoch than any earlier
    // generation or the clients would ignore the new election as stale. Only touched on the game thread
    // (election and migration both run under GameThread.RunSafe).
    private readonly Dictionary<string, int> issuedEpochs = new Dictionary<string, int>();

    public BattleHostHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        IBattleHostRegistry hostRegistry,
        IControllerIdProvider controllerIdProvider,
        IBattleTroopReserveBuilder reserveBuilder)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.hostRegistry = hostRegistry;
        this.controllerIdProvider = controllerIdProvider;
        this.reserveBuilder = reserveBuilder;

        messageBroker.Subscribe<PlayerEnteredBattle>(Handle_PlayerEnteredBattle);
        messageBroker.Subscribe<BattleMissionReady>(Handle_BattleMissionReady);
        messageBroker.Subscribe<NetworkRequestBattleHost>(Handle_NetworkRequestBattleHost);
        messageBroker.Subscribe<NetworkBattleHostAssigned>(Handle_NetworkBattleHostAssigned);
        messageBroker.Subscribe<MissionMemberDeparted>(Handle_MissionMemberDeparted);
        messageBroker.Subscribe<NetworkRequestBattleReserves>(Handle_NetworkRequestBattleReserves);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerEnteredBattle>(Handle_PlayerEnteredBattle);
        messageBroker.Unsubscribe<BattleMissionReady>(Handle_BattleMissionReady);
        messageBroker.Unsubscribe<NetworkRequestBattleHost>(Handle_NetworkRequestBattleHost);
        messageBroker.Unsubscribe<NetworkBattleHostAssigned>(Handle_NetworkBattleHostAssigned);
        messageBroker.Unsubscribe<MissionMemberDeparted>(Handle_MissionMemberDeparted);
        messageBroker.Unsubscribe<NetworkRequestBattleReserves>(Handle_NetworkRequestBattleReserves);
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
    /// parties resume cleanly. Empty sides are SKIPPED: before the election answers, an empty unowned side
    /// must not mark the requester's enemy-side supplier populated (sizing would run prematurely); the
    /// explicit empties arrive with the election reply instead.</summary>
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

            SendOwnedReserves(mapEventId, mapEvent, requester, requesterId, includeEmptySides: false);
        });
    }

    /// <summary>[Server] Send the requester every reserve it owns, one message per side. With
    /// <paramref name="includeEmptySides"/> a side the requester owns nothing on is sent as an explicit empty
    /// (election/migration replies — finalizes the side so sizing proceeds); without it, empty sides are
    /// skipped (entry replies — the unowned sides are not decided until the election).</summary>
    private void SendOwnedReserves(string mapEventId, MapEvent mapEvent, NetPeer requester, string requesterId, bool includeEmptySides)
    {
        if (requester == null) return;

        bool isHost = hostRegistry.TryGet(mapEventId, out var assignment)
            && assignment.HostControllerId == requesterId;

        foreach (var sideReserve in reserveBuilder.GetOwnedReserves(mapEvent, requesterId, isHost))
        {
            if (!includeEmptySides && (sideReserve.Parties == null || sideReserve.Parties.Length == 0))
                continue;

            network.Send(requester, new NetworkBattleTroopReserve(
                mapEventId, (int)sideReserve.Side, sideReserve.Parties));
        }
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
            if (!hostRegistry.TryGet(mapEventId, out var assignment))
                return; // no host assignment for this instance — not a battle

            // A retreat (graceful leave) despawned the departing player's troops, so forget their reserve party
            // — a rejoin then re-flattens it fresh (supplied pointer reset) and re-spawns. A disconnect keeps
            // the reserve (the host adopts the troops and reinforces them from the existing pointer).
            if (payload.What.WasRetreat && objectManager.TryGetObject<MapEvent>(mapEventId, out var mapEvent))
                reserveBuilder.ForgetController(mapEvent, controllerId);

            var successors = new List<string>(assignment.SuccessorControllerIds);

            if (assignment.HostControllerId == controllerId)
            {
                if (successors.Count == 0)
                {
                    Logger.Warning("[BattleHost] Host {Host} left battle {MapEventId} with no successors; clearing assignment",
                        controllerId, mapEventId);
                    hostRegistry.Remove(mapEventId);

                    // The battle is fully abandoned (no one left to continue it). Forget the WHOLE map event's
                    // reserves — not just the leaver's own party (ForgetController above) — so restarting the
                    // SAME map event re-flattens the AI/enemy parties the host had been fielding. Otherwise their
                    // supplied pointers stay at the end and only the host's own re-flattened party re-spawns.
                    if (objectManager.TryGetObject<MapEvent>(mapEventId, out var abandonedEvent))
                        reserveBuilder.ForgetMapEvent(abandonedEvent);
                    return;
                }

                // Promote the earliest-joined successor still present (the line is kept current as members leave).
                // BR-102: the host CHANGED, so the promotion opens the next hosting generation (epoch + 1).
                var newHost = successors[0];
                successors.RemoveAt(0);
                var promoted = new BattleHostAssignment(newHost, successors, NextEpoch(mapEventId, assignment.Epoch));
                hostRegistry.Set(mapEventId, promoted);

                Logger.Information("[BattleHost] Host {Old} left battle {MapEventId}; promoted {New} at epoch {Epoch} (successors: {Successors})",
                    controllerId, mapEventId, newHost, promoted.Epoch, string.Join(", ", successors));

                network.SendAll(ToMessage(mapEventId, promoted));
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
