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
using System.Linq;
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

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IPlayerManager playerManager;
    private readonly INetworkConfig configuration;
    private readonly IVillageHostileActionInterface villageHostileActionInterface;

    // The map-event id the server assigns is the round-trip reply; the "both sides attached" wait below is this
    // coordinator's own second phase, layered on top of the shared gate.
    private readonly BlockingRequestGate<string> gate = new BlockingRequestGate<string>();

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

        messageBroker.Subscribe<NetworkRequestCreateMapEvent>(Handle_NetworkRequestCreateMapEvent);
        messageBroker.Subscribe<NetworkMapEventCreated>(Handle_NetworkMapEventCreated);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkRequestCreateMapEvent>(Handle_NetworkRequestCreateMapEvent);
        messageBroker.Unsubscribe<NetworkMapEventCreated>(Handle_NetworkMapEventCreated);
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
            // keep pumping in case the reply is processed first. Resolving the bare MapEvent isn't enough: the parties'
            // side attachment lands via separate, GameThread-deferred messages sent right after, so wait until the
            // event is registered with the manager and both sides point at it, on the same deadline.
            MapEvent mapEvent = null;
            if (!GameThread.WaitWhilePumping(
                    () => objectManager.TryGetObject(pending.Reply, out mapEvent) && mapEvent != null
                        && Campaign.Current.MapEventManager.MapEvents.Contains(mapEvent)
                        && ReferenceEquals(attacker.MapEvent, mapEvent)
                        && ReferenceEquals(defender.MapEvent, mapEvent),
                    deadline))
            {
                Logger.Error(
                    "Server created map event {MapEventId} but it (or the attacker/defender side attachment) was not committed on this client before timeout. RequestId={RequestId}",
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
        {
            SendCreatedReply(requestingPeer, request, null);
            return;
        }

        if (!playerManager.TryGetPlayer(requestingPeer, out var player) ||
            !objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var requestingParty) ||
            (!ReferenceEquals(attacker.MobileParty, requestingParty) &&
             !ReferenceEquals(defender.MobileParty, requestingParty)))
        {
            SendCreatedReply(requestingPeer, request, null);
            return;
        }

        if (TryHandleExistingMapEventRequest(request, attacker, defender, requestingParty, out var existingMapEventId))
        {
            SendCreatedReply(requestingPeer, request, existingMapEventId);
            return;
        }

        if (!TryConsumeApprovedMapEventStart(request, attacker, defender))
        {
            SendCreatedReply(requestingPeer, request, null);
            return;
        }

        SendCreatedReply(requestingPeer, request, CreateMapEvent(request, attacker, defender));
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

        if (ReferenceEquals(attacker, defender) || request.Flags.IsForced) return true;

        if (attackerSide != null && defenderSide != null)
        {
            var attackerEvent = attackerSide.MapEvent;
            if (IsActiveFieldBattle(attackerEvent) &&
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
            joiningSide == null || joiningMobileParty?.IsActive != true ||
            joiningMobileParty.CurrentSettlement != null || !CanJoinFieldBattle(joiningParty, joiningSide))
            return true;

        joiningParty.MapEventSide = joiningSide;
        objectManager.TryGetIdWithLogging(mapEvent, out mapEventId);
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
