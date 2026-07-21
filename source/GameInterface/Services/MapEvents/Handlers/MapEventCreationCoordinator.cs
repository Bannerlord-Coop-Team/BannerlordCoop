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

        string expectedMapEventId = null;
        var expectedMapEvent = attacker.MapEvent ?? defender.MapEvent;
        if (expectedMapEvent != null && !objectManager.TryGetIdWithLogging(expectedMapEvent, out expectedMapEventId))
            return null;

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
            network.SendAll(new NetworkRequestCreateMapEvent(
                requestId,
                attackerId,
                defenderId,
                flags,
                expectedMapEventId));

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

        var request = payload.What;
        var requestingPeer = payload.Who as NetPeer;
        string reservedControllerId = null;
        if (!string.IsNullOrEmpty(request.ExpectedMapEventId) &&
            requestingPeer != null &&
            playerManager.TryGetPlayer(requestingPeer, out var player))
        {
            reservedControllerId = player.ControllerId;
            messageBroker.Publish(
                requestingPeer,
                new BattleJoinAccepted(request.ExpectedMapEventId, reservedControllerId));
        }

        GameThread.RunSafe(
            () =>
            {
                bool joined = false;
                try
                {
                    joined = CreateAndReplyToMapEventRequest(payload);
                }
                finally
                {
                    if (!joined && reservedControllerId != null)
                    {
                        messageBroker.Publish(
                            requestingPeer,
                            new BattleJoinCancelled(request.ExpectedMapEventId, reservedControllerId));
                    }
                }
            },
            blocking: true,
            context: nameof(Handle_NetworkRequestCreateMapEvent));
    }

    private bool CreateAndReplyToMapEventRequest(MessagePayload<NetworkRequestCreateMapEvent> payload)
    {
        var request = payload.What;
        if (!TryGetRequestingPeer(payload, request, out var requestingPeer))
            return false;

        if (!TryResolveRequestParties(request, out var attacker, out var defender))
        {
            SendCreatedReply(requestingPeer, request, null);
            return false;
        }

        if (!playerManager.TryGetPlayer(requestingPeer, out var player) ||
            !objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var requestingParty) ||
            (!ReferenceEquals(attacker.MobileParty, requestingParty) &&
             !ReferenceEquals(defender.MobileParty, requestingParty)))
        {
            SendCreatedReply(requestingPeer, request, null);
            return false;
        }

        if (TryHandleExistingMapEventRequest(
                request,
                attacker,
                defender,
                requestingParty,
                out var existingMapEventId,
                out var joinedExistingBattle))
        {
            if (joinedExistingBattle)
            {
                messageBroker.Publish(
                    requestingPeer,
                    new BattleJoinAccepted(existingMapEventId, player.ControllerId));
            }

            SendCreatedReply(requestingPeer, request, existingMapEventId);
            return joinedExistingBattle;
        }

        if (!TryConsumeApprovedMapEventStart(request, attacker, defender))
        {
            SendCreatedReply(requestingPeer, request, null);
            return false;
        }

        SendCreatedReply(requestingPeer, request, CreateMapEvent(request, attacker, defender));
        return false;
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
        out string mapEventId,
        out bool joinedExistingBattle)
    {
        mapEventId = null;
        joinedExistingBattle = false;
        var attackerSide = attacker.MapEventSide;
        var defenderSide = defender.MapEventSide;
        if (attackerSide == null && defenderSide == null)
            return !string.IsNullOrEmpty(request.ExpectedMapEventId);

        if (ReferenceEquals(attacker, defender) || request.Flags.IsForced) return true;

        if (attackerSide != null && defenderSide != null)
        {
            var attackerEvent = attackerSide.MapEvent;
            if (IsActiveFieldBattle(attackerEvent) &&
                IsExpectedMapEvent(request, attackerEvent) &&
                ReferenceEquals(attackerEvent, defenderSide.MapEvent) &&
                ReferenceEquals(attackerSide.OtherSide, defenderSide))
                objectManager.TryGetIdWithLogging(attackerEvent, out mapEventId);
            return true;
        }

        var occupiedSide = attackerSide ?? defenderSide;
        var joiningParty = attackerSide == null ? attacker : defender;
        var mapEvent = occupiedSide?.MapEvent;
        var joiningSide = occupiedSide?.OtherSide;
        var joiningMobileParty = joiningParty.MobileParty;
        if (!ReferenceEquals(joiningMobileParty, requestingParty) || !IsActiveFieldBattle(mapEvent) ||
            !IsExpectedMapEvent(request, mapEvent) ||
            joiningSide == null || joiningMobileParty?.IsActive != true ||
            joiningMobileParty.CurrentSettlement != null || !CanJoinFieldBattle(joiningParty, joiningSide))
            return true;

        joiningParty.MapEventSide = joiningSide;
        joinedExistingBattle = objectManager.TryGetIdWithLogging(mapEvent, out mapEventId);
        return true;
    }

    private bool IsExpectedMapEvent(NetworkRequestCreateMapEvent request, MapEvent mapEvent)
    {
        if (string.IsNullOrEmpty(request.ExpectedMapEventId))
            return true;

        return objectManager.TryGetId(mapEvent, out var mapEventId) && mapEventId == request.ExpectedMapEventId;
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
