using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.Locations;
using GameInterface.Services.Locations.Hosting;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using Missions.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace Missions.Locations;

/// <summary>
/// Server-authoritative election of the settlement-location NPC host (and successor order), plus the
/// client-side receipt of that assignment. Mirrors the election half of <c>BattleHostHandler</c>; there
/// is no reserve/ledger half — settlement NPCs have no troop supply. Lives in the Missions stack and
/// runs on both sides because MissionModule is registered into the client and the server containers.
/// <para>
/// Client: once the location mission has FINISHED LOADING (<see cref="LocationMissionReady"/>, published
/// by <c>CoopLocationsController</c>) it asks the server to elect via
/// <see cref="NetworkRequestLocationHost"/> and stores the reply
/// (<see cref="NetworkLocationHostAssigned"/>), driving <see cref="LocationNpcGate"/>.
/// Server: the first MISSION-READY client becomes the instance's host (SR-010); later ready clients
/// append to the successor line in mission-ready order. Departures promote the first successor
/// (SR-014) or clear the assignment when the instance empties (SR-016). Departures for instance ids
/// this registry does not hold (battles, tournaments) are ignored — that registry miss is the
/// instance-kind discriminator between this handler and <c>BattleHostHandler</c>.
/// </para>
/// </summary>
internal class LocationHostHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<LocationHostHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly ILocationHostRegistry hostRegistry;
    private readonly IControllerIdProvider controllerIdProvider;

    // [Server] Highest host epoch ever issued per location instance (SR-016), retained across assignment
    // removal: clients keep their last assignment when an instance empties (only the server's entry is
    // removed), so a re-election for the SAME settlement location must issue a HIGHER epoch than any
    // earlier generation or the clients would ignore the new election as stale. Only touched on the game
    // thread (election and departure both run under GameThread.RunSafe).
    private readonly Dictionary<string, int> issuedEpochs = new Dictionary<string, int>();

    public LocationHostHandler(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        ILocationHostRegistry hostRegistry,
        IControllerIdProvider controllerIdProvider)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.hostRegistry = hostRegistry;
        this.controllerIdProvider = controllerIdProvider;

        messageBroker.Subscribe<LocationMissionReady>(Handle_LocationMissionReady);
        messageBroker.Subscribe<NetworkRequestLocationHost>(Handle_NetworkRequestLocationHost);
        messageBroker.Subscribe<NetworkLocationHostAssigned>(Handle_NetworkLocationHostAssigned);
        messageBroker.Subscribe<MissionMemberDeparted>(Handle_MissionMemberDeparted);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<LocationMissionReady>(Handle_LocationMissionReady);
        messageBroker.Unsubscribe<NetworkRequestLocationHost>(Handle_NetworkRequestLocationHost);
        messageBroker.Unsubscribe<NetworkLocationHostAssigned>(Handle_NetworkLocationHostAssigned);
        messageBroker.Unsubscribe<MissionMemberDeparted>(Handle_MissionMemberDeparted);
    }

    /// <summary>[Client] The location mission FINISHED LOADING (we are mission-ready, SR-010): ask the
    /// server to elect (or report) the NPC host. The server records these requests in arrival order, so
    /// its per-instance connection order is the mission-ready order.</summary>
    private void Handle_LocationMissionReady(MessagePayload<LocationMissionReady> payload)
    {
        if (ModInformation.IsServer) return;

        var instanceId = payload.What.InstanceId;
        if (string.IsNullOrEmpty(instanceId)) return;

        // On a client, SendAll targets the server (its only connected peer).
        network.SendAll(new NetworkRequestLocationHost(instanceId, controllerIdProvider.ControllerId));
        Logger.Information("[LocationHost] Mission ready — requested host election for location {InstanceId}", instanceId);
    }

    /// <summary>[Server] Elect the host (first MISSION-READY requester wins, SR-010) and append later
    /// ready requesters to the successor line in arrival order; broadcast the assignment. A duplicate/late
    /// requester just gets a re-confirm.</summary>
    private void Handle_NetworkRequestLocationHost(MessagePayload<NetworkRequestLocationHost> payload)
    {
        if (ModInformation.IsClient) return;

        var instanceId = payload.What.InstanceId;
        var requesterId = payload.What.ControllerId;
        var requester = payload.Who as NetPeer;

        // Reads campaign collections and the shared assignment, so run on the main thread. That also
        // serializes requests for one instance: the first the server processes becomes the host, the rest
        // append in arrival (= mission-ready) order, so concurrent requests cannot double-elect.
        GameThread.RunSafe(() =>
        {
            if (string.IsNullOrEmpty(instanceId) || string.IsNullOrEmpty(requesterId))
                return;

            if (!IsRequesterInInstanceSettlement(instanceId, requesterId))
            {
                Logger.Warning("[LocationHost] Ignoring host request for {InstanceId} from '{Requester}' (party not in the settlement)",
                    instanceId, requesterId);
                return;
            }

            if (hostRegistry.TryGet(instanceId, out var existing))
            {
                // Host already elected. Record this player in the successor line in mission-ready order
                // (idempotent), so migration can promote the earliest still present. A mid-mission joiner
                // lands here too.
                if (TryAppendSuccessor(existing, requesterId, out var updated))
                {
                    SetServerAssignment(instanceId, updated);
                    Logger.Information("[LocationHost] {Requester} joined location {InstanceId}; successor line: {Successors}",
                        requesterId, instanceId, string.Join(", ", updated.SuccessorControllerIds));
                }
                else if (requester != null)
                {
                    network.Send(requester, ToMessage(instanceId, existing));
                }
            }
            else
            {
                // SR-016: the election issues the instance's next hosting generation — epoch 1 for a fresh
                // visit, or one past the last generation if this settlement location was emptied and
                // re-entered.
                var epoch = NextEpoch(instanceId);
                var assignment = new LocationHostAssignment(requesterId, Array.Empty<string>(), epoch);
                SetServerAssignment(instanceId, assignment);

                Logger.Information("[LocationHost] Elected NPC host {Host} (first mission-ready) for location {InstanceId} at epoch {Epoch}",
                    requesterId, instanceId, epoch);
            }
        });
    }

    /// <summary>[Server] A member left or dropped from a mission instance. Only instances THIS registry
    /// holds are location missions — a battle's id misses and returns (the kind discriminator).</summary>
    private void Handle_MissionMemberDeparted(MessagePayload<MissionMemberDeparted> payload)
    {
        if (ModInformation.IsClient) return;

        var controllerId = payload.What.ControllerId;
        var instanceId = payload.What.InstanceId;
        var isInstanceEmpty = payload.What.IsInstanceEmpty;

        // Mutates the shared assignment, so run on the main thread (serializes with election).
        GameThread.RunSafe(() =>
        {
            if (!hostRegistry.TryGet(instanceId, out var assignment))
                return; // not a location instance this handler elected (or no election happened) — not ours

            // SR-016: no players remain in the mission instance — clear the assignment outright,
            // regardless of whether the departed controller was the recorded host. The epoch watermark
            // survives (issuedEpochs), so a re-entered settlement location elects at a higher epoch.
            if (isInstanceEmpty)
            {
                Logger.Information("[LocationHost] Location {InstanceId} instance is empty after {Controller} departed; clearing host assignment",
                    instanceId, controllerId);
                hostRegistry.Remove(instanceId);
                return;
            }

            var successors = new List<string>(assignment.SuccessorControllerIds);

            if (assignment.HostControllerId == controllerId)
            {
                if (successors.Count == 0)
                {
                    // The recorded host left and no mission-ready successor exists — but the instance is
                    // NOT empty here (the empty branch above already returned), so a participant is still
                    // LOADING and will become the eventual host via its own mission-ready request. Remove
                    // the now-hostless assignment so that request runs a fresh election (at a higher epoch).
                    Logger.Warning("[LocationHost] Host {Host} left location {InstanceId} with no ready successors (players still loading); clearing assignment for re-election",
                        controllerId, instanceId);
                    hostRegistry.Remove(instanceId);
                    return;
                }

                // Promote the earliest-joined successor still present (the line is kept current as members
                // leave). SR-016: the host CHANGED, so the promotion opens the next hosting generation.
                var newHost = successors[0];
                successors.RemoveAt(0);
                var promoted = new LocationHostAssignment(newHost, successors, NextEpoch(instanceId, assignment.Epoch));
                SetServerAssignment(instanceId, promoted);

                Logger.Information("[LocationHost] Host {Old} left location {InstanceId}; promoted {New} at epoch {Epoch} (successors: {Successors})",
                    controllerId, instanceId, newHost, promoted.Epoch, string.Join(", ", successors));
            }
            else if (successors.Remove(controllerId))
            {
                // Successor-line cleanup: the host did not change, so the epoch is unchanged (SR-016).
                var updated = new LocationHostAssignment(assignment.HostControllerId, successors, assignment.Epoch);
                SetServerAssignment(instanceId, updated);

                Logger.Information("[LocationHost] Successor {Controller} left location {InstanceId}; successor line now: {Successors}",
                    controllerId, instanceId, string.Join(", ", successors));
            }
        });
    }

    /// <summary>[Client] Store the server's host assignment for this location instance.</summary>
    private void Handle_NetworkLocationHostAssigned(MessagePayload<NetworkLocationHostAssigned> payload)
    {
        if (ModInformation.IsServer) return;

        var message = payload.What;

        // Capture the host we knew before applying the update, so we can detect a migration TO us.
        string previousHost = null;
        bool wasLocalHost = false;
        LocationHostAssignment previous = null;
        if (hostRegistry.TryGet(message.InstanceId, out previous))
        {
            // SR-016: assignments are ordered by their host epoch. One LOWER than what we already hold is
            // a stale/out-of-order broadcast (e.g. re-delivered around a migration) and must not overwrite
            // the newer assignment; an EQUAL epoch is a successor-line update for the same host and applies.
            if (message.Epoch < previous.Epoch)
            {
                Logger.Information("[LocationHost] Ignoring stale host assignment for {InstanceId}: epoch {Stale} < current {Current} (named {Host})",
                    message.InstanceId, message.Epoch, previous.Epoch, message.HostControllerId);
                return;
            }

            previousHost = previous.HostControllerId;
            wasLocalHost = previous.HostControllerId == controllerIdProvider.ControllerId;
        }

        var assignment = new LocationHostAssignment(
            message.HostControllerId,
            message.SuccessorControllerIds ?? Array.Empty<string>(),
            message.Epoch);
        hostRegistry.Set(message.InstanceId, assignment);

        bool isLocalHost = message.HostControllerId == controllerIdProvider.ControllerId;
        bool isMigrationToUs = previousHost != null
            && previousHost != message.HostControllerId
            && isLocalHost;

        // Drive the static patch gate — it ignores assignments for instances other than the active mission.
        LocationNpcGate.SetLocalHost(message.InstanceId, isLocalHost);

        if (isLocalHost && (!wasLocalHost || previous?.Epoch != message.Epoch))
            messageBroker.Publish(this, new LocationHostAuthorityAcquired(message.InstanceId, isMigrationToUs));

        Logger.Information("[LocationHost] Location {InstanceId} NPC host is {Host}{IsMe} at epoch {Epoch} (successors: {Successors})",
            message.InstanceId,
            message.HostControllerId,
            isLocalHost ? " (this client)" : "",
            message.Epoch,
            string.Join(", ", assignment.SuccessorControllerIds));

        // Migration: the host changed and it is now us — adopt the previous host's orphaned NPC puppets
        // so the settlement continues uninterrupted (the migrator does the actual adoption with the live
        // mission).
        if (isMigrationToUs)
        {
            Logger.Information("[LocationHost] Became NPC host of {InstanceId} via migration from {Old}", message.InstanceId, previousHost);
            messageBroker.Publish(this, new LocationHostMigrated(message.InstanceId, previousHost));
        }
    }

    // True if the requesting controller's player party is currently in the instance's settlement. The
    // server never opens location missions, so settlement membership (server-authoritative campaign
    // state) is the validation — the location analog of the battle election's party-in-map-event check.
    private bool IsRequesterInInstanceSettlement(string instanceId, string requesterId)
    {
        if (!LocationInstanceId.TryGetSettlementId(instanceId, out var settlementId))
            return false;

        if (!objectManager.TryGetObjectWithLogging<Settlement>(settlementId, out var settlement))
            return false;

        foreach (var player in playerManager.Players)
        {
            if (player.ControllerId != requesterId)
                continue;
            return objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var party)
                && party?.CurrentSettlement == settlement;
        }
        return false;
    }

    // Append the requester to the successor line unless it is already the host or already queued. Returns
    // the new immutable assignment via <paramref name="updated"/>, or false when nothing changed.
    private static bool TryAppendSuccessor(LocationHostAssignment existing, string requesterId, out LocationHostAssignment updated)
    {
        updated = null;
        if (existing.HostControllerId == requesterId)
            return false;
        foreach (var successor in existing.SuccessorControllerIds)
            if (successor == requesterId)
                return false;

        // The host did not change, so the assignment stays in the same hosting generation (SR-016).
        var successors = new List<string>(existing.SuccessorControllerIds) { requesterId };
        updated = new LocationHostAssignment(existing.HostControllerId, successors, existing.Epoch);
        return true;
    }

    // [Server] Issue the instance's next host epoch (SR-016): one past the highest ever issued for this
    // settlement location (and past <paramref name="floor"/>, the current assignment's epoch when
    // promoting), starting at 1. The watermark survives assignment removal so an emptied-and-re-entered
    // location cannot reuse an epoch (see the field's remarks).
    private int NextEpoch(string instanceId, int floor = 0)
    {
        issuedEpochs.TryGetValue(instanceId, out var last);
        var next = Math.Max(last, floor) + 1;
        issuedEpochs[instanceId] = next;
        return next;
    }

    private static NetworkLocationHostAssigned ToMessage(string instanceId, LocationHostAssignment assignment)
    {
        var successors = new string[assignment.SuccessorControllerIds.Count];
        for (int i = 0; i < successors.Length; i++)
            successors[i] = assignment.SuccessorControllerIds[i];

        return new NetworkLocationHostAssigned(instanceId, assignment.HostControllerId, successors, assignment.Epoch);
    }

    private void SetServerAssignment(string instanceId, LocationHostAssignment assignment)
    {
        hostRegistry.Set(instanceId, assignment);
        network.SendAll(ToMessage(instanceId, assignment));
    }
}
