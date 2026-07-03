using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages;
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
/// Client: on entering a battle (<see cref="PlayerEnteredBattle"/>) it asks the server to elect via
/// <see cref="NetworkRequestBattleHost"/>, and stores the reply (<see cref="NetworkBattleHostAssigned"/>).
/// Server: the first client to enter a battle becomes its host; later entrants append to the successor line
/// in join order (so migration promotes the earliest joiner still in the mission). The result is cached, so
/// a duplicate request just re-confirms, and every change is broadcast to all clients.
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
        messageBroker.Subscribe<NetworkRequestBattleHost>(Handle_NetworkRequestBattleHost);
        messageBroker.Subscribe<NetworkBattleHostAssigned>(Handle_NetworkBattleHostAssigned);
        messageBroker.Subscribe<MissionMemberDeparted>(Handle_MissionMemberDeparted);
        messageBroker.Subscribe<NetworkRequestBattleReserves>(Handle_NetworkRequestBattleReserves);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerEnteredBattle>(Handle_PlayerEnteredBattle);
        messageBroker.Unsubscribe<NetworkRequestBattleHost>(Handle_NetworkRequestBattleHost);
        messageBroker.Unsubscribe<NetworkBattleHostAssigned>(Handle_NetworkBattleHostAssigned);
        messageBroker.Unsubscribe<MissionMemberDeparted>(Handle_MissionMemberDeparted);
        messageBroker.Unsubscribe<NetworkRequestBattleReserves>(Handle_NetworkRequestBattleReserves);
    }

    /// <summary>[Client] Entering a battle: ask the server who the host is.</summary>
    private void Handle_PlayerEnteredBattle(MessagePayload<PlayerEnteredBattle> payload)
    {
        if (ModInformation.IsServer) return;

        if (!objectManager.TryGetIdWithLogging(payload.What.MapEvent, out var mapEventId))
            return;

        // On a client, SendAll targets the server (its only connected peer). Carry our controller id so the
        // server records join order (first to enter = host).
        network.SendAll(new NetworkRequestBattleHost(mapEventId, controllerIdProvider.ControllerId));
        Logger.Information("[BattleHost] Requested host election for battle {MapEventId}", mapEventId);
    }

    /// <summary>[Server] Elect the host (first requester wins) and append later requesters to the successor
    /// line in join order; broadcast the assignment. A duplicate/late requester just gets a re-confirm.</summary>
    private void Handle_NetworkRequestBattleHost(MessagePayload<NetworkRequestBattleHost> payload)
    {
        if (ModInformation.IsClient) return;

        var mapEventId = payload.What.MapEventId;
        var requesterId = payload.What.ControllerId;
        var requester = payload.Who as NetPeer;

        // Reads campaign collections and the shared assignment, so run on the main thread. That also
        // serializes requests for one battle: the first the server processes becomes the host, the rest
        // append in arrival (= join) order, so concurrent requests cannot double-elect.
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
                var assignment = new BattleHostAssignment(requesterId, Array.Empty<string>());
                hostRegistry.Set(mapEventId, assignment);

                Logger.Information("[BattleHost] Elected host {Host} (first to join) for battle {MapEventId}", requesterId, mapEventId);

                network.SendAll(ToMessage(mapEventId, assignment));
            }

            // Feed the entrant the troop reserves it owns (its own party; plus the AI/enemy side if it is the
            // host) so its mission's troop supplier can spawn them. Buffered client-side until that supplier exists.
            SendOwnedReserves(mapEventId, mapEvent, requester, requesterId);
        });
    }

    /// <summary>[Server] A new owner (host adopting a leaver, or a promoted successor) asks for its now-larger
    /// reserve; reply with its full owned set at the current ledger pointers so adopted parties resume cleanly.</summary>
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

            SendOwnedReserves(mapEventId, mapEvent, requester, requesterId);
        });
    }

    /// <summary>[Server] Send the entrant every reserve it owns, one message per side.</summary>
    private void SendOwnedReserves(string mapEventId, MapEvent mapEvent, NetPeer requester, string requesterId)
    {
        if (requester == null) return;

        bool isHost = hostRegistry.TryGet(mapEventId, out var assignment)
            && assignment.HostControllerId == requesterId;

        foreach (var sideReserve in reserveBuilder.GetOwnedReserves(mapEvent, requesterId, isHost))
        {
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
                var newHost = successors[0];
                successors.RemoveAt(0);
                var promoted = new BattleHostAssignment(newHost, successors);
                hostRegistry.Set(mapEventId, promoted);

                Logger.Information("[BattleHost] Host {Old} left battle {MapEventId}; promoted {New} (successors: {Successors})",
                    controllerId, mapEventId, newHost, string.Join(", ", successors));

                network.SendAll(ToMessage(mapEventId, promoted));
            }
            else if (successors.Remove(controllerId))
            {
                var updated = new BattleHostAssignment(assignment.HostControllerId, successors);
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
        string previousHost = hostRegistry.TryGet(message.MapEventId, out var previous) ? previous.HostControllerId : null;

        var assignment = new BattleHostAssignment(
            message.HostControllerId,
            message.SuccessorControllerIds ?? Array.Empty<string>());
        hostRegistry.Set(message.MapEventId, assignment);

        Logger.Information("[BattleHost] Battle {MapEventId} host is {Host}{IsMe} (successors: {Successors})",
            message.MapEventId,
            message.HostControllerId,
            hostRegistry.IsHost(message.MapEventId) ? " (this client)" : "",
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

        var successors = new List<string>(existing.SuccessorControllerIds) { requesterId };
        updated = new BattleHostAssignment(existing.HostControllerId, successors);
        return true;
    }

    private static NetworkBattleHostAssigned ToMessage(string mapEventId, BattleHostAssignment assignment)
    {
        var successors = new string[assignment.SuccessorControllerIds.Count];
        for (int i = 0; i < successors.Length; i++)
            successors[i] = assignment.SuccessorControllerIds[i];

        return new NetworkBattleHostAssigned(mapEventId, assignment.HostControllerId, successors);
    }
}
