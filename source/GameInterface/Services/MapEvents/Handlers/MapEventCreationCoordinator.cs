using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.MapEvents.Logging;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Linq;
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

    /// <summary>
    /// Statically accessible instance so the (static) <c>StartBattleInternal</c> Harmony prefix can reach the
    /// DI-wired coordinator. Set on construction by the auto-activated handler registration.
    /// </summary>
    internal static MapEventCreationCoordinator Instance { get; private set; }

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly INetworkConfiguration configuration;
    private readonly ConcurrentDictionary<string, PendingRequest> pendingRequests = new ConcurrentDictionary<string, PendingRequest>();

    public MapEventCreationCoordinator(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        INetworkConfiguration configuration)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.configuration = configuration;

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

            if (!pending.Completed.Wait(timeout))
            {
                Logger.Error("Timed out after {Timeout} waiting for the server to create the map event. RequestId={RequestId}", timeout, requestId);
                return null;
            }

            if (string.IsNullOrEmpty(pending.MapEventId))
            {
                Logger.Error("Server reported that it could not create a map event. RequestId={RequestId}", requestId);
                return null;
            }

            // The MapEvent object is materialized on this client by the AutoRegistry create broadcast, which is sent
            // just before this response. Poll briefly in case the response is processed first.
            while (true)
            {
                if (objectManager.TryGetObject(pending.MapEventId, out MapEvent mapEvent) && mapEvent != null)
                {
                    Logger.Debug("Resolved server-created map event {MapEventId}. RequestId={RequestId}", pending.MapEventId, requestId);
                    return mapEvent;
                }

                if (DateTime.UtcNow >= deadline)
                {
                    Logger.Error(
                        "Server created map event {MapEventId} but it was not resolvable on this client before timeout. RequestId={RequestId}",
                        pending.MapEventId, requestId);
                    return null;
                }

                Thread.Sleep(5);
            }
        }
        finally
        {
            pendingRequests.TryRemove(requestId, out _);
        }
    }

    /// <summary>[Server] Create the MapEvent authoritatively and reply to the requesting client with its id.</summary>
    private void Handle_NetworkRequestCreateMapEvent(MessagePayload<NetworkRequestCreateMapEvent> payload)
    {
        if (!ModInformation.IsServer) return;

        var request = payload.What;

        if (!(payload.Who is NetPeer requestingPeer))
        {
            Logger.Error("Received {Message} with no originating peer. RequestId={RequestId}", nameof(NetworkRequestCreateMapEvent), request.RequestId);
            return;
        }

        if (!objectManager.TryGetObjectWithLogging<PartyBase>(request.AttackerId, out var attacker)) return;
        if (!objectManager.TryGetObjectWithLogging<PartyBase>(request.DefenderId, out var defender)) return;

        string mapEventId = null;

        // MapEvent creation mutates campaign state and must run on the server's main thread. The AllowedThread scope
        // lets the resulting StartBattleInternal/MapEvent construction run through unblocked by the mod's patches,
        // and registers the new MapEvent (broadcasting it to clients) before we read back its id.
        GameLoopRunner.RunOnMainThread(() =>
        {
            if (attacker.MobileParty?.IsPlayerParty() == true && 
                defender.MobileParty?.IsCurrentlyEngagingParty == true && 
                defender.MobileParty?.ShortTermTargetParty == attacker.MobileParty)
            {
                var temp = attacker;
                attacker = defender;
                defender = temp;
            }

            var mapEvent = MapEventBattleFactory.CreateMapEvent(attacker, defender, request.Flags);
            if (mapEvent == null) return;

            if (!objectManager.TryGetIdWithLogging(mapEvent, out mapEventId))
            {
                Logger.Error("Server created a map event but it has no registered id. RequestId={RequestId}", request.RequestId);
            }
        },
        blocking: true);

        if (string.IsNullOrEmpty(mapEventId))
        {
            // Intentionally do not respond; the client will time out and abort its battle start.
            Logger.Error("Server failed to create a map event for RequestId={RequestId}; not responding", request.RequestId);
            return;
        }

        Logger.Debug("Server created map event {MapEventId} for RequestId={RequestId}. Responding to client.", mapEventId, request.RequestId);

        network.Send(requestingPeer, new NetworkMapEventCreated(request.RequestId, mapEventId));
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
