using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Villages.Interfaces;
using LiteNetLib;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Handlers;

/// <summary>
/// Coordinates the blocking "client asks the server to create a battle's <see cref="MapEvent"/>" round trip.
/// </summary>
/// <remarks>
/// <para>
/// On the client, <see cref="RequestBlocking"/> sends a <see cref="NetworkRequestCreateMapEvent"/> and blocks the
/// calling (game main) thread until the server replies with a <see cref="NetworkMapEventCreated"/> (or it times out).
/// Blocking the main thread is safe here: the network is polled on a dedicated thread (see
/// <c>CoopNetworkBase.UpdateThread</c>), so the response — and the AutoRegistry create broadcast that actually
/// materializes the MapEvent on this client — are still received and dispatched while the main thread waits.
/// </para>
/// <para>
/// On the server, it handles <see cref="NetworkRequestCreateMapEvent"/> by creating the MapEvent authoritatively
/// (via <see cref="MapEventBattleFactory"/>), resolving its id through <see cref="IObjectManager"/>, and replying
/// to the requesting peer.
/// </para>
/// </remarks>
internal class MapEventCreationCoordinator : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapEventCreationCoordinator>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly INetworkConfig configuration;
    private readonly IVillageHostileActionInterface villageHostileActionInterface;

    // The map-event id the server assigns is the round-trip reply; the "both sides attached" wait below is this
    // coordinator's own second phase, layered on top of the shared gate.
    private readonly BlockingRequestGate<string> gate = new BlockingRequestGate<string>();

    public MapEventCreationCoordinator(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        INetworkConfig configuration,
        IVillageHostileActionInterface villageHostileActionInterface)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.configuration = configuration;
        this.villageHostileActionInterface = villageHostileActionInterface;

        messageBroker.Subscribe<NetworkRequestCreateMapEvent>(Handle_NetworkRequestCreateMapEvent);
        messageBroker.Subscribe<NetworkMapEventCreated>(Handle_NetworkMapEventCreated);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkRequestCreateMapEvent>(Handle_NetworkRequestCreateMapEvent);
        messageBroker.Unsubscribe<NetworkMapEventCreated>(Handle_NetworkMapEventCreated);
    }

    /// <summary>
    /// [Client] Blocks until the server creates the authoritative MapEvent for the given parties and it becomes
    /// resolvable on this client, then returns it. Returns null on timeout (caller should abort the battle start).
    /// </summary>
    public MapEvent RequestBlocking(PartyBase attacker, PartyBase defender, BattleCreationFlags flags)
    {
        if (attacker == null || defender == null)
        {
            Logger.Error("Cannot request map event creation with a null attacker or defender party");
            return null;
        }

        if (!objectManager.TryGetIdWithLogging(attacker, out var attackerId)) return null;
        if (!objectManager.TryGetIdWithLogging(defender, out var defenderId)) return null;

        var pending = gate.Register();

        try
        {
            var timeout = configuration.ObjectCreationTimeout;
            var deadline = DateTime.UtcNow + timeout;

            Logger.Debug(
                "Requesting authoritative map event creation from server. RequestId={RequestId}, AttackerId={AttackerId}, DefenderId={DefenderId}",
                pending.RequestId, attackerId, defenderId);

            // On a client, SendAll targets the server (its only connected peer).
            network.SendAll(new NetworkRequestCreateMapEvent(pending.RequestId, attackerId, defenderId, flags));

            // Phase one — the shared round trip: wait for the server's create reply, which carries the assigned
            // map-event id. This runs on the game-loop thread (the StartBattleInternal prefix, during a campaign
            // tick). A bare blocking wait here would stop the thread from pumping GameThread.Update, which the
            // network thread relies on: it processes messages in order, and a message ahead of the reply (e.g. a
            // NetworkMapEventFinalizeAttempted, applied through a blocking GameThread.Run) waits for that pump. The
            // gate's WaitWhilePumping keeps draining the queue while we wait so the network thread makes progress.
            if (!gate.WaitWhilePumping(pending, deadline))
            {
                Logger.Error("Timed out after {Timeout} waiting for the server to create the map event. RequestId={RequestId}", timeout, pending.RequestId);
                return null;
            }

            if (string.IsNullOrEmpty(pending.Reply))
            {
                Logger.Error("Server reported that it could not create a map event. RequestId={RequestId}", pending.RequestId);
                return null;
            }

            // Phase two — unique to map-event creation, layered on the shared gate: the MapEvent object is
            // materialized on this client by the AutoRegistry create broadcast, which is sent just before the reply;
            // keep pumping in case the reply is processed first. Resolving the bare MapEvent isn't enough: the
            // parties' side attachment lands via separate, GameThread-deferred messages sent right after, so wait for
            // those on the same deadline too.
            MapEvent mapEvent = null;
            bool BothSidesAttached() =>
                attacker.MapEventSide != null && defender.MapEventSide != null
                && attacker.MapEventSide.Parties.Any(p => p.Party == attacker)
                && defender.MapEventSide.Parties.Any(p => p.Party == defender)
                && (mapEvent.AttackerSide == attacker.MapEventSide || mapEvent.DefenderSide == attacker.MapEventSide)
                && (mapEvent.AttackerSide == defender.MapEventSide || mapEvent.DefenderSide == defender.MapEventSide);

            if (!GameThread.WaitWhilePumping(
                    () => objectManager.TryGetObject(pending.Reply, out mapEvent) && mapEvent != null
                        && BothSidesAttached(),
                    deadline))
            {
                Logger.Error(
                    "Server created map event {MapEventId} but it (or the attacker/defender side attachment) was not resolvable on this client before timeout. RequestId={RequestId}",
                    pending.Reply, pending.RequestId);
                return null;
            }

            Logger.Debug("Resolved server-created map event {MapEventId}. RequestId={RequestId}", pending.Reply, pending.RequestId);
            return mapEvent;
        }
        finally
        {
            gate.Release(pending);
        }
    }

    /// <summary>[Server] Create the MapEvent authoritatively and reply to the requesting client with its id.</summary>
    private void Handle_NetworkRequestCreateMapEvent(MessagePayload<NetworkRequestCreateMapEvent> payload)
    {
        if (ModInformation.IsClient) return;

        GameThread.RunSafe(
            () => CreateAndReplyToMapEventRequest(payload),
            blocking: true,
            context: nameof(Handle_NetworkRequestCreateMapEvent));
    }

    private void CreateAndReplyToMapEventRequest(MessagePayload<NetworkRequestCreateMapEvent> payload)
    {
        var request = payload.What;
        if (!TryGetRequestingPeer(payload, request, out var requestingPeer))
            return;

        if (!TryResolveRequestParties(request, out var attacker, out var defender))
            return;

        if (!TryConsumeApprovedMapEventStart(request, attacker, defender))
            return;

        string mapEventId = CreateMapEvent(request, attacker, defender);
        if (string.IsNullOrEmpty(mapEventId))
        {
            // Intentionally do not respond; the client will time out and abort its battle start.
            Logger.Error("Server failed to create a map event for RequestId={RequestId}; not responding", request.RequestId);
            return;
        }

        Logger.Debug("Server created map event {MapEventId} for RequestId={RequestId}. Responding to client.", mapEventId, request.RequestId);

        network.Send(requestingPeer, new NetworkMapEventCreated(request.RequestId, mapEventId));
    }

    private static bool TryGetRequestingPeer(
        MessagePayload<NetworkRequestCreateMapEvent> payload,
        NetworkRequestCreateMapEvent request,
        out NetPeer requestingPeer)
    {
        requestingPeer = payload.Who as NetPeer;
        if (requestingPeer != null)
            return true;

        Logger.Error("Received {Message} with no originating peer. RequestId={RequestId}", nameof(NetworkRequestCreateMapEvent), request.RequestId);
        return false;
    }

    private bool TryResolveRequestParties(
        NetworkRequestCreateMapEvent request,
        out PartyBase attacker,
        out PartyBase defender)
    {
        attacker = null;
        defender = null;

        if (!objectManager.TryGetObjectWithLogging<PartyBase>(request.AttackerId, out attacker))
            return false;

        return objectManager.TryGetObjectWithLogging<PartyBase>(request.DefenderId, out defender);
    }

    private bool TryConsumeApprovedMapEventStart(
        NetworkRequestCreateMapEvent request,
        PartyBase attacker,
        PartyBase defender)
    {
        if (villageHostileActionInterface.TryConsumeApprovedMapEventStart(attacker, defender, request.Flags, out var reason))
            return true;

        Logger.Warning(
            "Rejecting hostile-action map event creation. RequestId={RequestId}, AttackerId={AttackerId}, DefenderId={DefenderId}, Reason={Reason}",
            request.RequestId,
            request.AttackerId,
            request.DefenderId,
            reason);
        return false;
    }

    private string CreateMapEvent(
        NetworkRequestCreateMapEvent request,
        PartyBase attacker,
        PartyBase defender)
    {
        string mapEventId = null;

        var parties = GetMapEventParties(attacker, defender);
        var mapEvent = MapEventBattleFactory.CreateMapEvent(parties.Attacker, parties.Defender, request.Flags);
        if (mapEvent == null) return null;

        if (mapEvent.IsVillageHostileAction())
            MapEventHostileActionConsequences.Apply(mapEvent, parties.Attacker, "village hostile action start");

        if (!objectManager.TryGetIdWithLogging(mapEvent, out mapEventId))
        {
            Logger.Error("Server created a map event but it has no registered id. RequestId={RequestId}", request.RequestId);
        }

        return mapEventId;
    }

    private static (PartyBase Attacker, PartyBase Defender) GetMapEventParties(PartyBase attacker, PartyBase defender)
    {
        if (attacker.MobileParty?.IsPlayerParty() == true &&
            defender.MobileParty?.IsCurrentlyEngagingParty == true &&
            defender.MobileParty?.ShortTermTargetParty == attacker.MobileParty)
        {
            return (defender, attacker);
        }

        return (attacker, defender);
    }

    /// <summary>[Client] Complete the pending blocking request with the server-assigned MapEvent id.</summary>
    private void Handle_NetworkMapEventCreated(MessagePayload<NetworkMapEventCreated> payload)
    {
        var message = payload.What;

        if (!gate.Complete(message.RequestId, message.MapEventId))
        {
            // Late arrival (already timed out and removed) or a response for another instance.
            Logger.Warning("Received {Message} for unknown or expired RequestId={RequestId}", nameof(NetworkMapEventCreated), message.RequestId);
        }
    }
}
