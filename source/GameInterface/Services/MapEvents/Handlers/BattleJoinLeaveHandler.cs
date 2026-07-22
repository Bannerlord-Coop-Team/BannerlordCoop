using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MapEvents.Logging;
using GameInterface.Services.MapEvents.Initialization;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
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
    private readonly IMapEventLogger mapEventLogger;
    private readonly IMapEventInitializationBarrier initializationBarrier;

    public BattleJoinLeaveHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IMapEventLogger mapEventLogger,
        IMapEventInitializationBarrier initializationBarrier)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.mapEventLogger = mapEventLogger;
        this.initializationBarrier = initializationBarrier;

        messageBroker.Subscribe<NetworkAddInvolvedParties>(Handle_NetworkAddInvolvedParties);
        messageBroker.Subscribe<PlayerJoinBattleAttempted>(Handle_PlayerJoinBattleAttempted);
        messageBroker.Subscribe<NetworkRequestJoinBattle>(Handle_NetworkRequestJoinBattle);
        messageBroker.Subscribe<PlayerLeaveBattleAttempted>(Handle_PlayerLeaveBattleAttempted);
        messageBroker.Subscribe<NetworkRequestLeaveBattle>(Handle_NetworkRequestLeaveBattle);
        messageBroker.Subscribe<NetworkPartyLeftBattle>(Handle_NetworkPartyLeftBattle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<NetworkAddInvolvedParties>(Handle_NetworkAddInvolvedParties);
        messageBroker.Unsubscribe<PlayerJoinBattleAttempted>(Handle_PlayerJoinBattleAttempted);
        messageBroker.Unsubscribe<NetworkRequestJoinBattle>(Handle_NetworkRequestJoinBattle);
        messageBroker.Unsubscribe<PlayerLeaveBattleAttempted>(Handle_PlayerLeaveBattleAttempted);
        messageBroker.Unsubscribe<NetworkRequestLeaveBattle>(Handle_NetworkRequestLeaveBattle);
        messageBroker.Unsubscribe<NetworkPartyLeftBattle>(Handle_NetworkPartyLeftBattle);
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

                // Map-event commit assigns PartyBase.MapEventSide after the involved-party snapshot.
                initializationBarrier.RunAfterCommit(mapEvent, () => GameThread.EnqueueSafe(
                    () => RefreshCurrentEncounterMenu(mapEvent),
                    context: nameof(Handle_NetworkAddInvolvedParties)));
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply {Message}", nameof(NetworkAddInvolvedParties));
            }
        });
    }

    private static void RefreshCurrentEncounterMenu(MapEvent mapEvent)
    {
        var menuContext = Campaign.Current?.CurrentMenuContext;
        if (menuContext?.GameMenu?.StringId != "encounter" ||
            (PlayerEncounter.Battle != mapEvent && MobileParty.MainParty?.MapEvent != mapEvent)) return;

        menuContext.Handler?.OnMenuRefresh();
    }

    /// <summary>[Client] Bridge the local player's battle join to a server request.</summary>
    private void Handle_PlayerJoinBattleAttempted(MessagePayload<PlayerJoinBattleAttempted> payload)
    {
        var data = payload.What;

        if (!objectManager.TryGetIdWithLogging(data.MapEvent, out var mapEventId)) return;
        if (!objectManager.TryGetIdWithLogging(data.JoiningParty, out var partyId)) return;

        mapEventLogger.DebugMapEvent(data.MapEvent, "Requesting server to join battle. PartyId={PartyId}, Side={Side}", partyId, data.Side);

        // On a client, SendAll targets the server (its only connected peer).
        network.SendAll(new NetworkRequestJoinBattle(mapEventId, partyId, data.Side));
    }

    /// <summary>[Server] Perform the authoritative join; the native add replicates to all clients.</summary>
    private void Handle_NetworkRequestJoinBattle(MessagePayload<NetworkRequestJoinBattle> payload)
    {
        if (ModInformation.IsClient) return;

        var data = payload.What;

        GameThread.RunSafe(
            () =>
            {
                if (!objectManager.TryGetObjectWithLogging<MapEvent>(data.MapEventId, out var mapEvent)) return;
                if (!objectManager.TryGetObjectWithLogging<PartyBase>(data.PartyId, out var party)) return;

                if (party.MapEventSide != null)
                {
                    Logger.Warning("Ignoring join request: party {PartyId} is already in a map event", data.PartyId);
                    return;
                }
                if (mapEvent.IsActiveSlowVillageRaid() && data.Side == BattleSideEnum.Defender)
                {
                    Logger.Warning("Ignoring defender join request: map event {MapEventId} is an active slow village raid", data.MapEventId);
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
                if (mapEvent.IsVillageHostileAction() && data.Side == BattleSideEnum.Attacker)
                    MapEventHostileActionConsequences.Apply(mapEvent, party, "village hostile action attacker join");

                // If this battle is being auto-resolved, pull the joiner into the simulation instead of leaving it stuck in
                // the encounter menu. A ForwardingBattleObserver on the event means a server-driven simulation is running.
                // Sent after the add above so the joiner applies the replicated battle-party add (and so builds its own
                // party into its scoreboard) before this open arrives; the simulation handler then opens it as a spectator.
                if (mapEvent.BattleObserver is ForwardingBattleObserver && !mapEvent.IsUnsupportedMultiPlayerHostileAction())
                    network.SendAll(new NetworkOpenBattleSimulation(data.MapEventId));
            },
            blocking: true,
            context: nameof(Handle_NetworkRequestJoinBattle));
    }

    /// <summary>[Client] Bridge a joiner's leave to a server request; [Server] perform it directly.</summary>
    private void Handle_PlayerLeaveBattleAttempted(MessagePayload<PlayerLeaveBattleAttempted> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.LeavingParty, out var partyId)) return;

        if (ModInformation.IsServer)
            RemovePartyFromBattleAndBroadcast(partyId);
        else
            network.SendAll(new NetworkRequestLeaveBattle(partyId));
    }

    /// <summary>[Server] A client asked to leave a battle without ending it.</summary>
    private void Handle_NetworkRequestLeaveBattle(MessagePayload<NetworkRequestLeaveBattle> payload)
    {
        if (ModInformation.IsClient) return;

        RemovePartyFromBattleAndBroadcast(payload.What.PartyId);
    }

    // Single-party removal does not auto-replicate (RemovePartyInternal uses RemoveAt, bypassing the
    // collection sync), so remove authoritatively and broadcast the removal explicitly.
    private void RemovePartyFromBattleAndBroadcast(string partyId)
    {
        GameThread.RunSafe(
            () =>
            {
                if (!objectManager.TryGetObjectWithLogging<PartyBase>(partyId, out var party)) return;

                ApplyAuthoritativeLeave(party);
                network.SendAll(new NetworkPartyLeftBattle(partyId));
            },
            blocking: true,
            context: nameof(RemovePartyFromBattleAndBroadcast));
    }

    /// <summary>[Client] Apply a joiner's removal from its map event side.</summary>
    private void Handle_NetworkPartyLeftBattle(MessagePayload<NetworkPartyLeftBattle> payload)
    {
        var partyId = payload.What.PartyId;

        GameThread.RunSafe(
            () =>
            {
                if (Campaign.Current == null) return;
                if (!objectManager.TryGetObjectWithLogging<PartyBase>(partyId, out var party)) return;

                ApplyNetworkLeave(party);
            },
            context: nameof(Handle_NetworkPartyLeftBattle));
    }

    // Authoritative campaign logic runs with patches live so removal, finalization, and replication stay ordered.
    private static void ApplyAuthoritativeLeave(PartyBase party)
    {
        if (party.MapEventSide != null)
            party.MapEventSide = null;
    }

    // Apply the received removal under AllowedThread and close this client's encounter UI when appropriate.
    // PlayerEncounter.Finish is safe here: with MapEventSide already cleared, LeaveBattle no longer finalizes.
    private static void ApplyNetworkLeave(PartyBase party)
    {
        using (new AllowedThread())
        {
            if (party.MapEventSide != null)
                party.MapEventSide = null;

            if (party == PartyBase.MainParty && PlayerEncounter.Current != null)
                PlayerEncounter.Finish(false);
        }
    }
}
