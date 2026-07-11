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
using System.Collections.Concurrent;
using System.Threading;
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
/// <c>CoopNetworkBase.UpdateThread</c>), so the response and the aggregate initialization message that materializes
/// the complete MapEvent graph on this client are still received and dispatched while the main thread waits.
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

    /// <summary>
    /// Statically accessible instance so the (static) <c>StartBattleInternal</c> Harmony prefix can reach the
    /// DI-wired coordinator. Set on construction by the auto-activated handler registration.
    /// </summary>
    internal static MapEventCreationCoordinator Instance { get; private set; }

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly INetworkConfig configuration;
    private readonly IVillageHostileActionInterface villageHostileActionInterface;
    private readonly ConcurrentDictionary<string, PendingRequest> pendingRequests = new ConcurrentDictionary<string, PendingRequest>();

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

        Instance = this;

        messageBroker.Subscribe<NetworkRequestCreateMapEvent>(Handle_NetworkRequestCreateMapEvent);
        messageBroker.Subscribe<NetworkMapEventCreated>(Handle_NetworkMapEventCreated);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkRequestCreateMapEvent>(Handle_NetworkRequestCreateMapEvent);
        messageBroker.Unsubscribe<NetworkMapEventCreated>(Handle_NetworkMapEventCreated);

        if (Instance == this) Instance = null;
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

        var requestId = Guid.NewGuid().ToString();
        var pending = new PendingRequest();
        pendingRequests[requestId] = pending;

        try
        {
            var timeout = configuration.ObjectCreationTimeout;
            var deadline = DateTime.UtcNow + timeout;

            Logger.Debug(
                "Requesting authoritative map event creation from server. RequestId={RequestId}, AttackerId={AttackerId}, DefenderId={DefenderId}",
                requestId, attackerId, defenderId);

            // On a client, SendAll targets the server (its only connected peer).
            network.SendAll(new NetworkRequestCreateMapEvent(requestId, attackerId, defenderId, flags));

            // This runs on the game-loop thread (the StartBattleInternal prefix, during a campaign tick). A
            // bare blocking wait here stops the thread from pumping GameThread.Update, which the network
            // thread relies on: it processes messages in order, and a message ahead of the reply (e.g. a
            // NetworkMapEventFinalizeAttempted, applied through a blocking GameThread.Run) waits for that
            // pump. The reply — and the aggregate initialization that materializes the MapEvent — then sits
            // behind a handler that can never complete, so the wait always times out under battle load.
            // GameThread.WaitWhilePumping keeps draining the queue while we wait so the network thread makes
            // progress (it falls back to a plain poll when not on the game-loop thread).
            if (!GameThread.WaitWhilePumping(() => pending.Completed.IsSet, deadline))
            {
                Logger.Error("Timed out after {Timeout} waiting for the server to create the map event. RequestId={RequestId}", timeout, requestId);
                return null;
            }

            if (string.IsNullOrEmpty(pending.MapEventId))
            {
                Logger.Error("Server reported that it could not create a map event. RequestId={RequestId}", requestId);
                return null;
            }

            // The aggregate initialization is sent before the reply, but its atomic game-thread apply may still
            // be queued when the network thread signals completion. Registration is the commit point: once the
            // MapEvent resolves, its sides, parties, rosters, component, tracker, and visual are already attached.
            MapEvent mapEvent = null;
            if (!GameThread.WaitWhilePumping(
                    () => objectManager.TryGetObject(pending.MapEventId, out mapEvent) && mapEvent != null,
                    deadline))
            {
                Logger.Error(
                    "Server created map event {MapEventId} but its initialized graph was not resolvable on this client before timeout. RequestId={RequestId}",
                    pending.MapEventId, requestId);
                return null;
            }

            Logger.Debug("Resolved server-created map event {MapEventId}. RequestId={RequestId}", pending.MapEventId, requestId);
            return mapEvent;
        }
        finally
        {
            pendingRequests.TryRemove(requestId, out _);
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

        if (!pendingRequests.TryGetValue(message.RequestId, out var pending))
        {
            // Late arrival (already timed out and removed) or a response for another instance.
            Logger.Warning("Received {Message} for unknown or expired RequestId={RequestId}", nameof(NetworkMapEventCreated), message.RequestId);
            return;
        }

        pending.MapEventId = message.MapEventId;
        pending.Completed.Set();
    }

    /// <summary>
    /// Tracks a single in-flight request. <see cref="Completed"/> is deliberately not disposed: the network thread
    /// may signal it concurrently with the requesting thread giving up, and a low-frequency battle event does not
    /// justify the extra synchronization to dispose it safely.
    /// </summary>
    private sealed class PendingRequest
    {
        public ManualResetEventSlim Completed { get; } = new ManualResetEventSlim(false);
        public string MapEventId { get; set; }
    }
}
