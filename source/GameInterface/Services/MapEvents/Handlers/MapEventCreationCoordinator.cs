using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Initialization;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.Villages.Interfaces;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Threading;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Handlers;

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
    private readonly IMapEventInitializationBarrier initializationBarrier;
    private readonly ConcurrentDictionary<string, PendingRequest> pendingRequests = new ConcurrentDictionary<string, PendingRequest>();

    public MapEventCreationCoordinator(
        IMessageBroker messageBroker,
        INetwork network,
        IObjectManager objectManager,
        IPlayerManager playerManager,
        INetworkConfig configuration,
        IVillageHostileActionInterface villageHostileActionInterface,
        IMapEventInitializationBarrier initializationBarrier)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.playerManager = playerManager;
        this.configuration = configuration;
        this.villageHostileActionInterface = villageHostileActionInterface;
        this.initializationBarrier = initializationBarrier;

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
    /// on this client. Returns null on timeout.
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

            // Keep processing queued packet work while the game thread waits for the reply.
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

        if (!TryGetRequestingPlayerParty(requestingPeer, request, attacker, defender, out var requestingParty))
            return;

        if (TryHandleExistingMapEventRequest(request, attacker, defender, requestingParty, out var existingMapEventId))
        {
            if (!string.IsNullOrEmpty(existingMapEventId))
                SendCreatedReply(requestingPeer, request, existingMapEventId);
            return;
        }

        if (!TryConsumeApprovedMapEventStart(request, attacker, defender))
            return;

        string mapEventId = CreateMapEvent(request, attacker, defender);
        if (string.IsNullOrEmpty(mapEventId))
        {
            // Intentionally do not respond; the client will time out and abort its battle start.
            Logger.Error("Server failed to create a map event for RequestId={RequestId}; not responding", request.RequestId);
            return;
        }

        SendCreatedReply(requestingPeer, request, mapEventId);
    }

    private void SendCreatedReply(NetPeer requestingPeer, NetworkRequestCreateMapEvent request, string mapEventId)
    {
        Logger.Debug("Server resolved map event {MapEventId} for RequestId={RequestId}. Responding to client.", mapEventId, request.RequestId);
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

    private bool TryHandleExistingMapEventRequest(
        NetworkRequestCreateMapEvent request,
        PartyBase attacker,
        PartyBase defender,
        MobileParty requestingParty,
        out string mapEventId)
    {
        mapEventId = null;
        var attackerSide = attacker.MapEventSide;
        var defenderSide = defender.MapEventSide;
        if (attackerSide == null && defenderSide == null)
            return false;

        if (ReferenceEquals(attacker, defender))
        {
            LogIncompatibleExistingRequest(request, "attacker and defender are the same party");
            return true;
        }

        if (HasForcedCreationFlags(request))
        {
            LogIncompatibleExistingRequest(request, "forced battle requests cannot reuse or join an existing map event");
            return true;
        }

        if (attackerSide != null && defenderSide != null)
        {
            var attackerEvent = attackerSide.MapEvent;
            if (attackerEvent == null ||
                !ReferenceEquals(attackerEvent, defenderSide.MapEvent) ||
                !ReferenceEquals(attackerSide.OtherSide, defenderSide) ||
                !attackerEvent.IsFieldBattle ||
                attackerEvent.BattleState != BattleState.None ||
                attackerEvent.IsFinalized)
            {
                LogIncompatibleExistingRequest(request, "parties are not on opposing sides of the same active map event");
                return true;
            }

            if (!objectManager.TryGetIdWithLogging(attackerEvent, out mapEventId))
                mapEventId = null;
            return true;
        }

        var occupiedSide = attackerSide ?? defenderSide;
        var joiningParty = attackerSide == null ? attacker : defender;
        if (!ReferenceEquals(joiningParty.MobileParty, requestingParty))
        {
            LogIncompatibleExistingRequest(request, "the requester does not control the joining party");
            return true;
        }

        var mapEvent = occupiedSide?.MapEvent;
        var joiningSide = occupiedSide?.OtherSide;
        if (mapEvent == null || !mapEvent.IsFieldBattle || mapEvent.BattleState != BattleState.None ||
            mapEvent.IsFinalized || joiningSide == null)
        {
            LogIncompatibleExistingRequest(request, "the existing map event cannot accept another party");
            return true;
        }

        var joiningMobileParty = joiningParty.MobileParty;
        if (joiningMobileParty == null || !joiningMobileParty.IsActive || joiningMobileParty.CurrentSettlement != null)
        {
            LogIncompatibleExistingRequest(request, "the joining party is not an active mobile party on the map");
            return true;
        }

        if (!CanJoinFieldBattle(joiningParty, joiningSide))
        {
            LogIncompatibleExistingRequest(request, "the joining party is not faction-compatible with the battle sides");
            return true;
        }

        joiningParty.MapEventSide = joiningSide;

        if (!objectManager.TryGetIdWithLogging(mapEvent, out mapEventId))
            mapEventId = null;
        return true;
    }

    private static bool CanJoinFieldBattle(PartyBase party, MapEventSide side)
    {
        if (party?.MapFaction == null || side?.OtherSide == null)
            return false;

        return IsFactionCompatible(side, party.MapFaction, shouldBeAtWar: false) &&
            IsFactionCompatible(side.OtherSide, party.MapFaction, shouldBeAtWar: true);
    }

    private static bool IsFactionCompatible(MapEventSide side, IFaction joiningFaction, bool shouldBeAtWar)
    {
        foreach (var mapEventParty in side.Parties)
        {
            var involvedParty = mapEventParty?.Party;
            if (involvedParty?.MapFaction == null || !involvedParty.IsActive ||
                VillageHostileFactionStanceHelper.HasWarStance(involvedParty.MapFaction, joiningFaction) != shouldBeAtWar)
                return false;
        }

        return true;
    }

    private static bool HasForcedCreationFlags(NetworkRequestCreateMapEvent request) =>
        request.ForceRaid ||
        request.ForceSallyOut ||
        request.ForceVolunteers ||
        request.ForceSupplies ||
        request.IsSallyOutAmbush ||
        request.ForceBlockadeAttack ||
        request.ForceBlockadeSallyOutAttack ||
        request.ForceHideoutSendTroops;

    private static void LogIncompatibleExistingRequest(NetworkRequestCreateMapEvent request, string reason)
    {
        Logger.Warning(
            "Rejecting overlapping map event creation. RequestId={RequestId}, AttackerId={AttackerId}, DefenderId={DefenderId}, Reason={Reason}",
            request.RequestId,
            request.AttackerId,
            request.DefenderId,
            reason);
    }

    private bool TryGetRequestingPlayerParty(
        NetPeer requestingPeer,
        NetworkRequestCreateMapEvent request,
        PartyBase attacker,
        PartyBase defender,
        out MobileParty requestingParty)
    {
        requestingParty = null;
        if (playerManager.TryGetPlayer(requestingPeer, out var player) &&
            objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out requestingParty) &&
            (ReferenceEquals(attacker.MobileParty, requestingParty) ||
             ReferenceEquals(defender.MobileParty, requestingParty)))
        {
            return true;
        }

        Logger.Warning(
            "Rejecting unauthorized map event creation. RequestId={RequestId}, AttackerId={AttackerId}, DefenderId={DefenderId}, Peer={Peer}",
            request.RequestId,
            request.AttackerId,
            request.DefenderId,
            requestingPeer.Id);
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

        initializationBarrier.CommitServer(mapEvent);

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
