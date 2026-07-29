using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Villages.Interfaces;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Handlers;

internal readonly struct MapEventCreationResult
{
    public MapEventCreationOutcome Outcome { get; }
    public MapEvent MapEvent { get; }

    private MapEventCreationResult(MapEventCreationOutcome outcome, MapEvent mapEvent)
    {
        Outcome = outcome;
        MapEvent = mapEvent;
    }

    public static MapEventCreationResult Created(MapEvent mapEvent) =>
        new MapEventCreationResult(MapEventCreationOutcome.Created, mapEvent);

    public static MapEventCreationResult Rejected() =>
        new MapEventCreationResult(MapEventCreationOutcome.Rejected, null);

    public static MapEventCreationResult Unresolved() =>
        new MapEventCreationResult(MapEventCreationOutcome.Unresolved, null);
}

/// <summary>
/// Coordinates server-authoritative MapEvent creation and client-side publication.
/// </summary>
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
    private readonly IPlayerManager playerManager;
    private readonly INetworkConfig configuration;
    private readonly IVillageHostileActionInterface villageHostileActionInterface;
#if DEBUG
    private readonly MapEventCreationDebugHook debugHook = new MapEventCreationDebugHook();
#endif
    private readonly ConcurrentDictionary<string, PendingRequest> pendingRequests = new ConcurrentDictionary<string, PendingRequest>();

    public MapEventCreationCoordinator(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        INetworkConfig configuration,
        IVillageHostileActionInterface villageHostileActionInterface)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
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
    /// [Client] Blocks until the server creates the authoritative MapEvent and its initialization is committed
    /// on this client. Only a reply marked rejected unwinds the encounter; registry failures and timeouts remain
    /// unresolved because the authoritative outcome is unknown.
    /// </summary>
    public MapEventCreationResult RequestBlocking(PartyBase attacker, PartyBase defender, BattleCreationFlags flags)
    {
        if (attacker == null || defender == null)
        {
            Logger.Error("Cannot request map event creation with a null attacker or defender party");
            return MapEventCreationResult.Unresolved();
        }

        if (!objectManager.TryGetIdWithLogging(attacker, out var attackerId))
            return MapEventCreationResult.Unresolved();
        if (!objectManager.TryGetIdWithLogging(defender, out var defenderId))
            return MapEventCreationResult.Unresolved();

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

            // Keep processing queued packet work while the game thread waits for the reply.
            if (!GameThread.WaitWhilePumping(() => pending.Completed.IsSet, deadline))
            {
                Logger.Error("Timed out after {Timeout} waiting for the server to create the map event. RequestId={RequestId}", timeout, requestId);
                return MapEventCreationResult.Unresolved();
            }

            if (pending.Outcome == MapEventCreationOutcome.Rejected)
            {
                Logger.Error("Server reported that it could not create a map event. RequestId={RequestId}", requestId);
                return MapEventCreationResult.Rejected();
            }

            if (pending.Outcome != MapEventCreationOutcome.Created ||
                string.IsNullOrEmpty(pending.MapEventId))
            {
                Logger.Error("Server could not resolve the authoritative map event. RequestId={RequestId}", requestId);
                return MapEventCreationResult.Unresolved();
            }

            // The reply can wake this request before the queued initialization commit has run.
            MapEvent mapEvent = null;
            if (!GameThread.WaitWhilePumping(
                    () => objectManager.TryGetObject(pending.MapEventId, out mapEvent) && mapEvent != null
                        && Campaign.Current.MapEventManager.MapEvents.Contains(mapEvent)
                        && ReferenceEquals(attacker.MapEvent, mapEvent)
                        && ReferenceEquals(defender.MapEvent, mapEvent),
                    deadline))
            {
                Logger.Error(
                    "Server created map event {MapEventId} but it was not committed on this client before timeout. RequestId={RequestId}",
                    pending.MapEventId, requestId);
                return MapEventCreationResult.Unresolved();
            }

            Logger.Debug("Resolved server-created map event {MapEventId}. RequestId={RequestId}", pending.MapEventId, requestId);
            return MapEventCreationResult.Created(mapEvent);
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
        {
            SendCreatedReply(requestingPeer, request, MapEventCreationOutcome.Rejected, null);
            return;
        }

        if (!playerManager.TryGetPlayer(requestingPeer, out var player) ||
            !objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var requestingParty) ||
            (!ReferenceEquals(attacker.MobileParty, requestingParty) &&
             !ReferenceEquals(defender.MobileParty, requestingParty)))
        {
            SendCreatedReply(requestingPeer, request, MapEventCreationOutcome.Rejected, null);
            return;
        }

#if DEBUG
        if (debugHook.TryConsume(request))
        {
            Logger.Warning(
                "DEBUG rejected map event creation after ownership validation. RequestId={RequestId}, AttackerId={AttackerId}, DefenderId={DefenderId}",
                request.RequestId,
                request.AttackerId,
                request.DefenderId);
            SendCreatedReply(requestingPeer, request, MapEventCreationOutcome.Rejected, null);
            return;
        }
#endif

        if (TryHandleExistingMapEventRequest(
                request,
                attacker,
                defender,
                requestingParty,
                out var existingOutcome,
                out var existingMapEventId))
        {
            SendCreatedReply(requestingPeer, request, existingOutcome, existingMapEventId);
            return;
        }

        if (!TryConsumeApprovedMapEventStart(request, attacker, defender))
        {
            SendCreatedReply(requestingPeer, request, MapEventCreationOutcome.Rejected, null);
            return;
        }

        var creationResult = CreateMapEvent(request, attacker, defender);
        SendCreatedReply(requestingPeer, request, creationResult.Outcome, creationResult.MapEventId);
    }

    private void SendCreatedReply(
        NetPeer requestingPeer,
        NetworkRequestCreateMapEvent request,
        MapEventCreationOutcome outcome,
        string mapEventId)
    {
        Logger.Debug(
            "Server resolved map event request with {Outcome} and {MapEventId}. RequestId={RequestId}",
            outcome,
            mapEventId,
            request.RequestId);
        network.Send(requestingPeer, new NetworkMapEventCreated(request.RequestId, outcome, mapEventId));
    }

#if DEBUG
    internal string ArmDebugRejection(string attackerId, string defenderId)
    {
        debugHook.Arm(attackerId, defenderId);
        return debugHook.Describe();
    }

    internal string GetDebugRejectionState() => debugHook.Describe();

    internal void ClearDebugRejection() => debugHook.Clear();
#endif

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

    private bool TryHandleExistingMapEventRequest(
        NetworkRequestCreateMapEvent request,
        PartyBase attacker,
        PartyBase defender,
        MobileParty requestingParty,
        out MapEventCreationOutcome outcome,
        out string mapEventId)
    {
        outcome = MapEventCreationOutcome.Rejected;
        mapEventId = null;
        var attackerSide = attacker.MapEventSide;
        var defenderSide = defender.MapEventSide;
        if (attackerSide == null && defenderSide == null)
            return false;

        if (ReferenceEquals(attacker, defender) || request.Flags.IsForced) return true;

        if (attackerSide != null && defenderSide != null)
        {
            var attackerEvent = attackerSide.MapEvent;
            if (IsActiveFieldBattle(attackerEvent) &&
                ReferenceEquals(attackerEvent, defenderSide.MapEvent) &&
                ReferenceEquals(attackerSide.OtherSide, defenderSide))
            {
                outcome = objectManager.TryGetIdWithLogging(attackerEvent, out mapEventId)
                    ? MapEventCreationOutcome.Created
                    : MapEventCreationOutcome.Unresolved;
            }
            return true;
        }

        var occupiedSide = attackerSide ?? defenderSide;
        var joiningParty = attackerSide == null ? attacker : defender;
        var mapEvent = occupiedSide?.MapEvent;
        var joiningSide = occupiedSide?.OtherSide;
        var joiningMobileParty = joiningParty.MobileParty;
        if (!ReferenceEquals(joiningMobileParty, requestingParty) || !IsActiveFieldBattle(mapEvent) ||
            joiningSide == null || joiningMobileParty?.IsActive != true ||
            joiningMobileParty.CurrentSettlement != null || !CanJoinFieldBattle(joiningParty, joiningSide))
            return true;

        joiningParty.MapEventSide = joiningSide;
        outcome = objectManager.TryGetIdWithLogging(mapEvent, out mapEventId)
            ? MapEventCreationOutcome.Created
            : MapEventCreationOutcome.Unresolved;
        return true;
    }

    private static bool IsActiveFieldBattle(MapEvent mapEvent) =>
        mapEvent?.IsFieldBattle == true && mapEvent.BattleState == BattleState.None && !mapEvent.IsFinalized;

    private static bool CanJoinFieldBattle(PartyBase party, MapEventSide side)
    {
        var faction = party?.MapFaction;
        return faction != null && side?.OtherSide != null &&
            side.Parties.All(x => IsFactionCompatible(x?.Party, faction, false)) &&
            side.OtherSide.Parties.All(x => IsFactionCompatible(x?.Party, faction, true));
    }

    private static bool IsFactionCompatible(PartyBase involved, IFaction joining, bool hostile) =>
        involved?.MapFaction != null && involved.IsActive &&
        VillageHostileFactionStanceHelper.HasWarStance(involved.MapFaction, joining) == hostile;

    private (MapEventCreationOutcome Outcome, string MapEventId) CreateMapEvent(
        NetworkRequestCreateMapEvent request,
        PartyBase attacker,
        PartyBase defender)
    {
        string mapEventId = null;

        var parties = GetMapEventParties(attacker, defender);
        var mapEvent = MapEventBattleFactory.CreateMapEvent(parties.Attacker, parties.Defender, request.Flags);
        if (mapEvent == null) return (MapEventCreationOutcome.Rejected, null);

        if (mapEvent.IsVillageHostileAction())
            MapEventHostileActionConsequences.Apply(mapEvent, parties.Attacker, "village hostile action start");

        if (!objectManager.TryGetIdWithLogging(mapEvent, out mapEventId))
        {
            Logger.Error("Server created a map event but it has no registered id. RequestId={RequestId}", request.RequestId);
            return (MapEventCreationOutcome.Unresolved, null);
        }

        return (MapEventCreationOutcome.Created, mapEventId);
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

        pending.Outcome = message.Outcome;
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
        public MapEventCreationOutcome Outcome { get; set; } = MapEventCreationOutcome.Unresolved;
        public string MapEventId { get; set; }
    }
}
