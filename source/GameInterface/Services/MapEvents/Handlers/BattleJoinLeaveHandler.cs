using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MapEvents.Extensions;
using GameInterface.Services.MapEvents.Logging;
using GameInterface.Services.MapEvents.Initialization;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using GameInterface.Services.SiegeEvents.Interfaces;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Concurrent;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Handlers;

/// <summary>
/// Owns a party joining or leaving a battle without ending it (split out of <see cref="BattleHandler"/>). A client
/// bridges its join/leave to a server request; the server performs it authoritatively and, for a single-party
/// removal that does not auto-replicate, broadcasts it. Also applies the server's involved-party snapshot on the
/// client (troop-upgrade tracking + position snap). The server-side involved-parties broadcast and the player-count
/// fast-forward bookkeeping stay in <see cref="BattleHandler"/> because they drive time control.
/// </summary>
internal class BattleJoinLeaveHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleJoinLeaveHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IPlayerManager playerManager;
    private readonly IMapEventLogger mapEventLogger;
    private readonly IMapEventInitializationBarrier initializationBarrier;
    private readonly ISiegeEventInterface siegeEventInterface;
    private readonly ISiegeMapEventLeaderReconciler siegeMapEventLeaderReconciler;
    private readonly ConcurrentDictionary<string, string> pendingJoinRequests = new ConcurrentDictionary<string, string>();

    public BattleJoinLeaveHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IPlayerManager playerManager,
        IMapEventLogger mapEventLogger,
        IMapEventInitializationBarrier initializationBarrier,
        ISiegeEventInterface siegeEventInterface,
        ISiegeMapEventLeaderReconciler siegeMapEventLeaderReconciler)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.playerManager = playerManager;
        this.mapEventLogger = mapEventLogger;
        this.initializationBarrier = initializationBarrier;
        this.siegeEventInterface = siegeEventInterface;
        this.siegeMapEventLeaderReconciler = siegeMapEventLeaderReconciler;

        messageBroker.Subscribe<NetworkAddInvolvedParties>(Handle_NetworkAddInvolvedParties);
        messageBroker.Subscribe<PlayerJoinBattleAttempted>(Handle_PlayerJoinBattleAttempted);
        messageBroker.Subscribe<NetworkRequestJoinBattle>(Handle_NetworkRequestJoinBattle);
        messageBroker.Subscribe<NetworkJoinBattleReply>(Handle_NetworkJoinBattleReply);
        messageBroker.Subscribe<PlayerLeaveBattleAttempted>(Handle_PlayerLeaveBattleAttempted);
        messageBroker.Subscribe<NetworkRequestLeaveBattle>(Handle_NetworkRequestLeaveBattle);
        messageBroker.Subscribe<NetworkPartyLeftBattle>(Handle_NetworkPartyLeftBattle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkAddInvolvedParties>(Handle_NetworkAddInvolvedParties);
        messageBroker.Unsubscribe<PlayerJoinBattleAttempted>(Handle_PlayerJoinBattleAttempted);
        messageBroker.Unsubscribe<NetworkRequestJoinBattle>(Handle_NetworkRequestJoinBattle);
        messageBroker.Unsubscribe<NetworkJoinBattleReply>(Handle_NetworkJoinBattleReply);
        messageBroker.Unsubscribe<PlayerLeaveBattleAttempted>(Handle_PlayerLeaveBattleAttempted);
        messageBroker.Unsubscribe<NetworkRequestLeaveBattle>(Handle_NetworkRequestLeaveBattle);
        messageBroker.Unsubscribe<NetworkPartyLeftBattle>(Handle_NetworkPartyLeftBattle);
        pendingJoinRequests.Clear();
    }

    private void Handle_NetworkAddInvolvedParties(MessagePayload<NetworkAddInvolvedParties> payload)
    {
        var message = payload.What;

        GameThread.RunSafe(() =>
        {
            try
            {
                // The campaign can tear down (exit to menu, disconnect, save load) between
                // enqueuing this and the main thread draining it; bail before touching
                // campaign state (the position snap below dereferences Campaign.Current).
                if (Campaign.Current == null)
                    return;

                if (!objectManager.TryGetObjectWithLogging<MapEvent>(message.MapEventId, out var mapEvent))
                    return;

                mapEventLogger.DebugMapEvent(mapEvent, "Handling network add involved parties. Party count: {MapEventPartyCount}", message.MapEventPartyIds.Length);

                var positions = message.Positions;

                var trackParties = !initializationBarrier.IsPending(mapEvent);
                using (new AllowedThread())
                {
                    for (int i = 0; i < message.MapEventPartyIds.Length; i++)
                    {
                        var mapEventPartyId = message.MapEventPartyIds[i];
                        if (!objectManager.TryGetObjectWithLogging<MapEventParty>(mapEventPartyId, out var mapEventParty))
                            continue;

                        if (trackParties)
                            mapEvent.TroopUpgradeTracker.AddParty(mapEventParty);
                        var mobileParty = mapEventParty.Party.MobileParty;
                        if (mobileParty != null && positions != null && i < positions.Length)
                            mobileParty.Position = positions[i];
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply {Message}", nameof(NetworkAddInvolvedParties));
            }
        });
    }

    /// <summary>[Client] Bridge the local player's battle join to a server request.</summary>
    private void Handle_PlayerJoinBattleAttempted(MessagePayload<PlayerJoinBattleAttempted> payload)
    {
        if (ModInformation.IsServer) return;

        var data = payload.What;

        if (!objectManager.TryGetIdWithLogging(data.MapEvent, out var mapEventId)) return;
        if (!objectManager.TryGetIdWithLogging(data.JoiningParty, out var partyId)) return;

        var requestId = Guid.NewGuid().ToString();
        if (!pendingJoinRequests.TryAdd(partyId, requestId))
        {
            mapEventLogger.DebugMapEvent(data.MapEvent, "Battle join is already pending for PartyId={PartyId}", partyId);
            return;
        }

        mapEventLogger.DebugMapEvent(data.MapEvent, "Requesting server to join battle. PartyId={PartyId}, Side={Side}", partyId, data.Side);

        // On a client, SendAll targets the server (its only connected peer).
        network.SendAll(new NetworkRequestJoinBattle(requestId, mapEventId, partyId, data.Side));
    }

    private void Handle_NetworkJoinBattleReply(MessagePayload<NetworkJoinBattleReply> payload)
    {
        if (ModInformation.IsServer) return;

        var reply = payload.What;
        GameThread.RunSafe(() =>
        {
            if (!pendingJoinRequests.TryGetValue(reply.PartyId, out var pendingRequestId) ||
                !string.Equals(pendingRequestId, reply.RequestId, StringComparison.Ordinal))
            {
                return;
            }

            pendingJoinRequests.TryRemove(reply.PartyId, out _);
            if (reply.Accepted) return;

            Logger.Warning("Server rejected battle join for party {PartyId} and map event {MapEventId}",
                reply.PartyId, reply.MapEventId);

            if (Campaign.Current == null) return;
            if (!objectManager.TryGetObjectWithLogging<MapEvent>(reply.MapEventId, out var mapEvent)) return;
            if (!objectManager.TryGetObjectWithLogging<PartyBase>(reply.PartyId, out var party)) return;

            var encounter = PlayerEncounter.Current;
            if (!ReferenceEquals(party, PartyBase.MainParty) ||
                party.MapEventSide != null ||
                mapEvent.FindMapEventParty(party) != null ||
                encounter == null ||
                !encounter.IsJoinedBattle ||
                !ReferenceEquals(encounter._mapEvent, mapEvent))
            {
                return;
            }

            PlayerEncounter.LeaveBattle();
            if (Campaign.Current.CurrentMenuContext != null)
                GameMenu.SwitchToMenu("join_encounter");
        }, context: nameof(Handle_NetworkJoinBattleReply));
    }

    /// <summary>[Server] Perform the authoritative join; the native add replicates to all clients.</summary>
    private void Handle_NetworkRequestJoinBattle(MessagePayload<NetworkRequestJoinBattle> payload)
    {
        if (ModInformation.IsClient) return;

        var data = payload.What;
        var requestingPeer = payload.Who as NetPeer;

        GameThread.RunSafe(
            () =>
            {
                bool joined = false;
                var reservationId = Guid.NewGuid();
                var reservedControllerId = ReserveJoin(requestingPeer, data.MapEventId, reservationId);
                try
                {
                    if (!objectManager.TryGetObjectWithLogging<MapEvent>(data.MapEventId, out var mapEvent)) return;
                    if (!objectManager.TryGetObjectWithLogging<PartyBase>(data.PartyId, out var party)) return;
                    if (!TryGetRequestingPlayer(requestingPeer, party, out _))
                    {
                        Logger.Warning("Ignoring join request: peer does not control party {PartyId}", data.PartyId);
                        return;
                    }

                    if (mapEvent.BattleState != BattleState.None || mapEvent.IsFinalized)
                    {
                        Logger.Warning("Ignoring join request: map event {MapEventId} is already concluded", data.MapEventId);
                        return;
                    }
                    if (party.MapEventSide != null)
                    {
                        Logger.Warning("Ignoring join request: party {PartyId} is already in a map event", data.PartyId);
                        return;
                    }
                    var side = mapEvent.GetMapEventSide(data.Side);
                    if (side == null)
                    {
                        Logger.Warning("Ignoring join request: map event {MapEventId} has no side {Side}", data.MapEventId, data.Side);
                        return;
                    }

                    // The setter runs the native MapEventSide.AddPartyInternal on the server (NOT under AllowedThread), so the
                    // AddIntercept publishes the battle-party add and it replicates to every client through the map-event sync.
                    party.MapEventSide = side;
                    joined = mapEvent.FindMapEventParty(party) != null;
                    if (!joined)
                    {
                        Logger.Error("Battle join did not create a MapEventParty for party {PartyId} in map event {MapEventId}",
                            data.PartyId, data.MapEventId);
                        return;
                    }

                    // Removal temporarily promotes a remaining party; put the persistent besieger back when it rejoins.
                    siegeMapEventLeaderReconciler.RestoreAfterJoin(mapEvent, party);

                    if (mapEvent.IsVillageHostileAction() && data.Side == BattleSideEnum.Attacker)
                        MapEventHostileActionConsequences.Apply(mapEvent, party, "village hostile action attacker join");

                    // The original mode broadcast predates this join. Replay it after the party add so the joining
                    // client applies membership first and rebuilds its encounter menu with the authoritative mode.
                    if (requestingPeer != null && ServerBattleModeArbiter.TryGetMode(data.MapEventId, out var mode))
                        network.Send(requestingPeer, new NetworkBattleModeSet(data.MapEventId, (int)mode));

                    // If this battle is being auto-resolved, pull the joiner into the simulation instead of leaving it stuck in
                    // the encounter menu. A ForwardingBattleObserver on the event means a server-driven simulation is running.
                    // Sent after the add above so the joiner applies the replicated battle-party add (and so builds its own
                    // party into its scoreboard) before this open arrives; the simulation handler then opens it as a spectator.
                    if (mapEvent.BattleObserver is ForwardingBattleObserver && !mapEvent.IsUnsupportedMultiPlayerHostileAction())
                        network.SendAll(new NetworkOpenBattleSimulation(data.MapEventId));
                }
                finally
                {
                    if (!joined && reservedControllerId != null)
                        PublishJoinCancelled(requestingPeer, data.MapEventId, reservedControllerId, reservationId);

                    if (requestingPeer != null)
                    {
                        network.Send(requestingPeer, new NetworkJoinBattleReply(
                            data.RequestId,
                            data.MapEventId,
                            data.PartyId,
                            joined));
                    }
                }
            },
            blocking: true,
            context: nameof(Handle_NetworkRequestJoinBattle));
    }

    /// <summary>[Client] Bridge a joiner's leave to a server request; [Server] perform it directly.</summary>
    private void Handle_PlayerLeaveBattleAttempted(MessagePayload<PlayerLeaveBattleAttempted> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.LeavingParty, out var partyId)) return;

        if (ModInformation.IsServer)
            RemovePartyFromBattleAndBroadcast(partyId, payload.What.FinishLocalMenus);
        else
            network.SendAll(new NetworkRequestLeaveBattle(partyId, payload.What.FinishLocalMenus));
    }

    /// <summary>[Server] A client asked to leave a battle without ending it.</summary>
    private void Handle_NetworkRequestLeaveBattle(MessagePayload<NetworkRequestLeaveBattle> payload)
    {
        if (ModInformation.IsClient) return;

        RemovePartyFromBattleAndBroadcast(
            payload.What.PartyId,
            payload.What.FinishLocalMenus,
            payload.Who as NetPeer);
    }

    // Single-party removal does not auto-replicate (RemovePartyInternal uses RemoveAt, bypassing the
    // collection sync), so remove authoritatively and broadcast the removal explicitly.
    private void RemovePartyFromBattleAndBroadcast(
        string partyId,
        bool finishLocalMenus = true,
        NetPeer requestingPeer = null)
    {
        GameThread.RunSafe(
            () =>
            {
                if (!objectManager.TryGetObjectWithLogging<PartyBase>(partyId, out var party)) return;

                var mapEvent = party.MapEvent;
                bool leaveSiege = IsAttackingSiegeAssault(party);
                ApplyAuthoritativeLeave(party);
                // Preserve the client's PlayerSiege reference until its explicit cleanup runs.
                network.SendAll(new NetworkPartyLeftBattle(
                    partyId,
                    leaveSiege,
                    finishLocalMenus));

                if (leaveSiege && party.MobileParty?.BesiegerCamp != null)
                    party.MobileParty.BesiegerCamp = null;

                if (mapEvent != null &&
                    objectManager.TryGetId(mapEvent, out var mapEventId) &&
                    TryGetRequestingPlayer(requestingPeer, party, out var controllerId))
                {
                    messageBroker.Publish(
                        requestingPeer,
                        new BattleJoinCancelled(mapEventId, controllerId));
                }
            },
            blocking: true,
            context: nameof(RemovePartyFromBattleAndBroadcast));
    }

    /// <summary>[Client] Apply a joiner's removal from its map event side.</summary>
    private void Handle_NetworkPartyLeftBattle(MessagePayload<NetworkPartyLeftBattle> payload)
    {
        var message = payload.What;

        GameThread.RunSafe(
            () =>
            {
                if (Campaign.Current == null) return;
                if (!objectManager.TryGetObjectWithLogging<PartyBase>(message.PartyId, out var party)) return;

                ApplyNetworkLeave(
                    party,
                    message.LeaveSiege,
                    message.FinishLocalMenus);
            },
            context: nameof(Handle_NetworkPartyLeftBattle));
    }

    // Authoritative campaign logic runs with patches live so removal, finalization, and replication stay ordered.
    private static void ApplyAuthoritativeLeave(PartyBase party)
    {
        if (party.MapEventSide != null)
            party.MapEventSide = null;
    }

    private string ReserveJoin(NetPeer requestingPeer, string mapEventId, Guid reservationId)
    {
        if (requestingPeer == null || !playerManager.TryGetPlayer(requestingPeer, out var player))
            return null;

        messageBroker.Publish(requestingPeer,
            new BattleJoinAccepted(mapEventId, player.ControllerId, reservationId));
        return player.ControllerId;
    }

    private void PublishJoinCancelled(
        NetPeer requestingPeer,
        string mapEventId,
        string controllerId,
        Guid reservationId)
    {
        messageBroker.Publish(
            requestingPeer,
            new BattleJoinCancelled(mapEventId, controllerId, reservationId));
    }

    private bool TryGetRequestingPlayer(
        NetPeer requestingPeer,
        PartyBase party,
        out string controllerId)
    {
        controllerId = null;
        if (requestingPeer == null ||
            !playerManager.TryGetPlayer(requestingPeer, out var player) ||
            !objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var playerParty) ||
            !ReferenceEquals(playerParty.Party, party))
        {
            return false;
        }

        controllerId = player.ControllerId;
        return true;
    }

    private static bool IsAttackingSiegeAssault(PartyBase party)
    {
        return party.MapEvent?.IsSiegeAssault == true && party.Side == BattleSideEnum.Attacker;
    }

    // Apply the received removal under AllowedThread and close this client's encounter UI when appropriate.
    private void ApplyNetworkLeave(PartyBase party, bool leaveSiege, bool finishLocalMenus)
    {
        using (new AllowedThread())
        {
            var mapEvent = party.MapEvent;
            bool isSiegeAssault = mapEvent?.IsSiegeAssault == true;
            var siegeSettlement = mapEvent?.MapEventSettlement;
            bool isMainParty = party == PartyBase.MainParty;
            var mobileParty = party.MobileParty;
            var tracker = isMainParty ? mapEvent?.TroopUpgradeTracker : null;

            if (party.MapEventSide != null)
                party.MapEventSide = null;

            // Vanilla discards the client's tracker when its MainParty leaves, but the server's registered tracker stays live.
            if (tracker != null && mapEvent?.IsFinalized == false && mapEvent.TroopUpgradeTracker == null)
            {
                mapEvent.TroopUpgradeTracker = tracker;
                tracker._mapEventParties.Clear();
            }

            if (leaveSiege && mobileParty?.BesiegerCamp != null)
                mobileParty.BesiegerCamp = null;

            if (isMainParty && finishLocalMenus)
            {
                if (leaveSiege || isSiegeAssault)
                {
                    siegeEventInterface.FinishLocalPlayerSiegeLeave(
                        siegeSettlement,
                        forcePlayerOutFromSettlement: false);
                }
                else if (PlayerEncounter.Current != null)
                {
                    PlayerEncounter.Finish(false);
                }
            }

            if (leaveSiege && isMainParty)
                mobileParty?.SetMoveModeHold();
        }
    }
}
