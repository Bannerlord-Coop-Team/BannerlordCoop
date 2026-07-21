using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using Coop.Core.Server.Services.Time.Messages;
using GameInterface.Services.GameDebug.Messages;
using GameInterface.Services.Heroes.Enum;
using GameInterface.Services.Heroes.Interaces;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.MapEventParties;
using GameInterface.Services.MapEventParties.Messages;
using GameInterface.Services.MapEvents.Logging;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.MapEvents.TroopSupply;
using GameInterface.Services.MapEvents.TroopSupply.Messages;
using GameInterface.Services.MobileParties.Extensions;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Map;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Handlers;

internal class BattleHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<BattleHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IMapEventLogger mapEventLogger;
    private readonly IPlayerManager playerRegistry;
    private readonly ITimeControlInterface timeControlInterface;
    private readonly IBattleTroopReserveBuilder battleTroopReserveBuilder;

    // Server-side: number of players in a map event at the last broadcast, used to
    // detect when fast-forward becomes (un)available and to keep clients informed.
    private int lastBroadcastPlayersInMapEvent;

    public BattleHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        IMapEventLogger mapEventLogger,
        IPlayerManager playerRegistry,
        ITimeControlInterface timeControlInterface,
        IBattleTroopReserveBuilder battleTroopReserveBuilder)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.mapEventLogger = mapEventLogger;
        this.playerRegistry = playerRegistry;
        this.timeControlInterface = timeControlInterface;
        this.battleTroopReserveBuilder = battleTroopReserveBuilder;
        messageBroker.Subscribe<PlayerJoinedBattle>(Handle_PlayerJoinedBattle);

        messageBroker.Subscribe<MapEventInvolvedPartiesAdded>(Handle_MapEventInvolvedPartiesAdded);

        messageBroker.Subscribe<NetworkBattleModeSet>(Handle_NetworkBattleModeSet);

        messageBroker.Subscribe<MapEventFinalized>(Handle_MapEventFinalized);
        messageBroker.Subscribe<TimeSpeedChangedAttempted>(Handle_TimeSpeedChangedAttempted);

        timeControlInterface.AddFastForwardPolicy(FastForwardWhilePlayerInMapEventPolicy);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<PlayerJoinedBattle>(Handle_PlayerJoinedBattle);

        messageBroker.Unsubscribe<MapEventInvolvedPartiesAdded>(Handle_MapEventInvolvedPartiesAdded);

        messageBroker.Unsubscribe<NetworkBattleModeSet>(Handle_NetworkBattleModeSet);

        messageBroker.Unsubscribe<MapEventFinalized>(Handle_MapEventFinalized);
        messageBroker.Unsubscribe<TimeSpeedChangedAttempted>(Handle_TimeSpeedChangedAttempted);

        timeControlInterface.RemoveFastForwardPolicy(FastForwardWhilePlayerInMapEventPolicy);
    }

    /// <summary>
    /// [Client] The server claimed a map event for a battle-resolution mode. If the local player's own party is in
    /// that event, record the mode (<see cref="BattleModeRegistry"/>) so the encounter menu greys out the wrong-mode
    /// options (<see cref="Patches.BattleModeEncounterOptionsPatch"/>). Clients not in the event ignore it.
    /// </summary>
    private void Handle_NetworkBattleModeSet(MessagePayload<NetworkBattleModeSet> payload)
    {
        if (ModInformation.IsServer) return;

        var mapEventId = payload.What.MapEventId;
        var mode = (BattleStartMode)payload.What.Mode;

        GameThread.RunSafe(() =>
        {
            // A retreat briefly tears down the local encounter before reopening its menu. Apply an id-scoped clear
            // even while neither local encounter reference points at the event, otherwise the mission mode sticks.
            if (mode == BattleStartMode.Unclaimed)
            {
                if (BattleModeRegistry.End(mapEventId))
                    RefreshCurrentEncounterMenu();
                return;
            }

            if (!objectManager.TryGetObject(mapEventId, out MapEvent mapEvent) || mapEvent == null)
                return;

            if (MobileParty.MainParty?.MapEvent != mapEvent && PlayerEncounter.Battle != mapEvent)
                return;

            BattleModeRegistry.Begin(mapEventId, mode);
            RefreshCurrentEncounterMenu();
        });
    }

    private static void RefreshCurrentEncounterMenu()
    {
        var menuContext = Campaign.Current?.CurrentMenuContext;
        // The menu can activate before this queued update, so rebuild it with the new mode.
        if (menuContext?.GameMenu?.StringId == "encounter")
            menuContext.Refresh();
    }

    private void Handle_MapEventInvolvedPartiesAdded(MessagePayload<MapEventInvolvedPartiesAdded> payload)
    {
        // This message is published when a player party joins a battle (a fresh one
        // during MapEvent.Initialize, or an already-running one as a reinforcement);
        // the joining party's MapEventSide is already set by this point, so the live
        // count is accurate.
        CapFastForwardForMapEvent();
        RefreshFastForwardState();

        var message = payload.What;
        if (!objectManager.TryGetIdWithLogging(message.MapEvent, out var mapEventSideId))
            return;

        mapEventLogger.DebugMapEvent(message.MapEvent, "Map event involved parties added. Added party count: {AddedPartyCount}", message.AddedParties.Count());

        var partyIds = new List<string>();
        var partyPositions = new List<CampaignVec2>();
        var initialSpawnCounts = new List<int>();
        var postPlanAdditions = new List<bool>();

        foreach (var addedParty in message.AddedParties)
        {
            if (!objectManager.TryGetIdWithLogging(addedParty, out var mapEventPartyId))
                continue;

            partyIds.Add(mapEventPartyId);
            initialSpawnCounts.Add(battleTroopReserveBuilder.GrantUnassignedInitialSpawns(
                message.MapEvent, addedParty, out var isPostPlanAddition));
            postPlanAdditions.Add(isPostPlanAddition);
            // Capture the party's authoritative map position, in lockstep with the id and
            // before the roster check below so the two arrays stay index-aligned. Settlement
            // parties have no MobileParty; their slot is a default the client never applies.
            partyPositions.Add(addedParty.Party.MobileParty?.Position ?? default);

            // A player just created or joined this map event, so push every involved party's
            // flattened roster to clients (AI-only battles never reach here). Clients need these to
            // spawn troops in the mission; in-progress AI parties already have a roster built from
            // server simulation. Per-troop changes after this are kept in sync incrementally.
            if (addedParty._roster == null)
                continue;

            var flattenedTroops = FlattenedTroopSerializer.Serialize(addedParty._roster, objectManager);
            network.SendAll(new NetworkUpdateMapEventParty(mapEventPartyId, flattenedTroops));
        }

        network.SendAll(new NetworkAddInvolvedParties(
            mapEventSideId,
            partyIds.ToArray(),
            partyPositions.ToArray(),
            initialSpawnCounts.ToArray(),
            postPlanAdditions.ToArray()
        ));

        if (postPlanAdditions.Any(isAddition => isAddition))
            messageBroker.Publish(this, new BattleReserveScopeChanged(mapEventSideId));

        // Tell any player parties just added to the battle to drop their "hold on" PvP popup — the battle menu
        // blocks them now. Server-driven because the client-side MapEventInvolvedPartiesAdded never fires for a
        // synced add (the client's own add is intercepted and routed to the server).
        var playerPartyIds = new List<string>();
        foreach (var addedParty in message.AddedParties)
        {
            if (addedParty.Party?.MobileParty?.IsPlayerParty() == true && objectManager.TryGetId(addedParty.Party, out var playerPartyId))
                playerPartyIds.Add(playerPartyId);
        }

        if (playerPartyIds.Count > 0)
            network.SendAll(new NetworkHidePvpPopup(playerPartyIds.ToArray()));

        // The conversation is over once the battle map event forms, so release the PvP interaction block. Holding it
        // longer re-blocks the parties (and hangs the encounter menu) when they later leave the map event; the map
        // event itself keeps others out while the battle runs.
        foreach (var id in playerPartyIds)
            ConversationPartyTracker.Instance?.EndPvpConversation(id);
    }

    private void Handle_PlayerJoinedBattle(MessagePayload<PlayerJoinedBattle> payload)
    {
        // A player started a battle: cap fast-forward while any player is in a map event. Pausing once EVERY
        // player is occupied (in a map event OR settlement) is handled separately by PlayerOccupancyPauseHandler.
        CapFastForwardForMapEvent();
        RefreshFastForwardState();
    }

    private void Handle_MapEventFinalized(MessagePayload<MapEventFinalized> payload)
    {
        // A map event ended; its parties have left it, so re-evaluate whether
        // fast-forward should become available again.
        RefreshFastForwardState(finalizedMapEvent: payload.What.MapEvent);
    }

    private void Handle_TimeSpeedChangedAttempted(MessagePayload<TimeSpeedChangedAttempted> payload)
    {
        // Notify the host (clients are notified by their own time handler) when they
        // try to fast-forward while it is blocked by a map event.
        if (ModInformation.IsClient)
            return;

        if (payload.What.NewControlMode != TimeControlEnum.Play_2x)
            return;

        var playersInMapEvent = CountPlayersInMapEvents();
        if (playersInMapEvent == 0)
            return;

        messageBroker.Publish(this, new SendInformationMessage(
            MapEventTimeControlMessages.FastForwardBlocked(playersInMapEvent)));
    }

    /// <summary>
    /// Recomputes how many players are in a map event and, when that crosses the
    /// 0 / non-0 boundary, announces it locally and informs clients. Server only.
    /// </summary>
    /// <param name="finalizedMapEvent">A map event being finalized, excluded from the count.</param>
    private void RefreshFastForwardState(MapEvent finalizedMapEvent = null)
    {
        if (ModInformation.IsClient)
            return;

        var count = CountPlayersInMapEvents(finalizedMapEvent);
        if (count == lastBroadcastPlayersInMapEvent)
            return;

        var wasBlocked = lastBroadcastPlayersInMapEvent > 0;
        var isBlocked = count > 0;
        lastBroadcastPlayersInMapEvent = count;

        network.SendAll(new NetworkMapEventLockChanged(count));

        if (isBlocked && !wasBlocked)
            messageBroker.Publish(this, new SendInformationMessage(MapEventTimeControlMessages.FastForwardDisabled));
        else if (!isBlocked && wasBlocked)
            messageBroker.Publish(this, new SendInformationMessage(MapEventTimeControlMessages.FastForwardEnabled));
    }

    /// <summary>
    /// Drops the campaign out of fast-forward when a player is in a map event. The
    /// fast-forward policy then keeps it capped at normal speed until every player
    /// has left their map event. Runs on the server only.
    /// </summary>
    private void CapFastForwardForMapEvent()
    {
        if (ModInformation.IsClient)
            return;

        if (AnyPlayerInMapEvent() && timeControlInterface.GetTimeControl() == TimeControlEnum.Play_2x)
        {
            timeControlInterface.ServerSetTimeControl(TimeControlEnum.Play_1x);
        }
    }

    /// <summary>
    /// Fast-forwarding the campaign map is not allowed while any player is in a map event.
    /// </summary>
    /// <returns>True if fast-forwarding is allowed, otherwise false</returns>
    private bool FastForwardWhilePlayerInMapEventPolicy()
    {
        return AnyPlayerInMapEvent() == false;
    }

    private bool AnyPlayerInMapEvent() => CountPlayersInMapEvents() > 0;

    private int CountPlayersInMapEvents(MapEvent excluding = null)
    {
        // Backs the fast-forward policy and messaging, which are evaluated on every
        // time-control change, so this uses the non-logging lookup to avoid spamming
        // the log when a party is momentarily unresolved.
        return playerRegistry.Players.Count(player =>
        {
            if (!objectManager.TryGetObject<MobileParty>(player.MobilePartyId, out var playerParty))
                return false;

            return IsFastForwardBlockingMapEvent(playerParty.MapEvent, excluding);
        });
    }

    private static bool IsFastForwardBlockingMapEvent(MapEvent mapEvent, MapEvent excluding = null)
    {
        return mapEvent != null &&
               mapEvent != excluding &&
               !mapEvent.IsActiveSlowVillageRaid();
    }
}
