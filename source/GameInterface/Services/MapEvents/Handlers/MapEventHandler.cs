using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Logging;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using LiteNetLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;

namespace GameInterface.Services.MapEvents.Handlers;

internal class MapEventHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapEventHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IMapEventLogger mapEventLogger;
    private readonly IBattleHostRegistry hostRegistry;
    private readonly IPlayerManager playerManager;

    public MapEventHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager,
        IMapEventLogger mapEventLogger, IBattleHostRegistry hostRegistry, IPlayerManager playerManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.mapEventLogger = mapEventLogger;
        this.hostRegistry = hostRegistry;
        this.playerManager = playerManager;

        messageBroker.Subscribe<MapEventBattleStateChangeAttempted>(Handle_MapEventBattleStateChangeAttempted);
        messageBroker.Subscribe<NetworkChangeBattleState>(Handle_NetworkChangeBattleState);

        messageBroker.Subscribe<MapEventSurrenderAttempted>(Handle_MapEventSurrenderAttempted);
        messageBroker.Subscribe<NetworkMapEventSurrender>(Handle_NetworkMapEventSurrender);
    }

    public void Dispose()
    {

        messageBroker.Unsubscribe<MapEventBattleStateChangeAttempted>(Handle_MapEventBattleStateChangeAttempted);
        messageBroker.Unsubscribe<NetworkChangeBattleState>(Handle_NetworkChangeBattleState);

        messageBroker.Unsubscribe<MapEventSurrenderAttempted>(Handle_MapEventSurrenderAttempted);
        messageBroker.Unsubscribe<NetworkMapEventSurrender>(Handle_NetworkMapEventSurrender);
    }

    private void Handle_MapEventBattleStateChangeAttempted(MessagePayload<MapEventBattleStateChangeAttempted> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.MapEvent, out var mapEventId))
            return;

        mapEventLogger.DebugMapEventId(mapEventId,
            "Sending network battle state change. BattleState={BattleState}",
            payload.What.BattleState);

        // BR-102: a battle-state report exercises host authority, so stamp it with our current host epoch
        // for this battle. Battles without a host assignment (no coop mission) send 0 = unstamped.
        int hostEpoch = hostRegistry.TryGet(mapEventId, out var assignment) ? assignment.Epoch : 0;

        var message = new NetworkChangeBattleState(mapEventId, payload.What.BattleState, hostEpoch);
        network.SendAll(message);
    }

    private void Handle_NetworkChangeBattleState(MessagePayload<NetworkChangeBattleState> payload)
    {
        // Do NOT wrap in AllowedThread. Setting BattleState to a victory state runs the native setter ->
        // OnBattleWon -> CalculateAndCommitMapEventResults -> CaptureDefeatedPartyMembers, which is the
        // server-authoritative capture path. Under AllowedThread CallOriginalPolicy.IsOriginalAllowed() is
        // true, so the coop capture interceptors (PlayerStartCaptivityPatches, TakePrisonerActionPatches and
        // the PartyBelongedToAsPrisoner sync) all run the original and the capture is never replicated -
        // the client gets no capture UI and the player party is never parked. The server's BattleState
        // setter does not re-broadcast (MapEventPatches.Prefix_BattleState returns without publishing on the
        // server), so no AllowedThread is needed to prevent an echo.
        var mapEventId = payload.What.MapEventId;
        var battleState = payload.What.BattleState;
        var sender = payload.Who as NetPeer;
        GameThread.Run(() =>
        {
            bool applied = false;
            bool publishConclusion = false;
            MapEvent mapEvent = null;
            string[] playerPartyIds = Array.Empty<string>();
            try
            {
                if (!objectManager.TryGetObjectWithLogging(mapEventId, out mapEvent))
                    return;

                mapEventLogger.DebugMapEvent(mapEvent,
                    "Applying network battle state change. BattleState={BattleState}",
                    battleState);

                if (mapEvent.BattleState != BattleState.None)
                {
                    applied = mapEvent.BattleState == battleState;
                    mapEventLogger.DebugMapEvent(mapEvent,
                        "Ignoring network battle state change because battle is already concluded. CurrentBattleState={CurrentBattleState}, IncomingBattleState={IncomingBattleState}",
                        mapEvent.BattleState,
                        battleState);
                    return;
                }

            // Only the elected battle host's conclusion is authoritative: a non-host's local mission can
            // conclude a victory the shared battle never reached (its enemies arrive as another client's
            // puppets). Applying it would run the full capture/finalize on a battle still being fought.
                if ((battleState == BattleState.AttackerVictory || battleState == BattleState.DefenderVictory)
                    && hostRegistry.TryGet(mapEventId, out var hostAssignment))
                {
                    if (sender != null
                        && playerManager.TryGetPlayer(sender, out var sendingPlayer)
                        && sendingPlayer.ControllerId != hostAssignment.HostControllerId)
                    {
                        Logger.Information("Refused battle state {BattleState} for {MapEventId} from non-host {ControllerId}",
                            battleState, mapEventId, sendingPlayer.ControllerId);
                        return;
                    }

                // BR-102: even the correct sender may hold stale authority — a report stamped with the epoch
                // of an earlier hosting stint (in flight across a migration, or from a former host that has
                // since been re-promoted) must not conclude the battle. Only a report stamped with the
                // CURRENT assignment's epoch is honored.
                    if (payload.What.HostEpoch != hostAssignment.Epoch)
                    {
                        Logger.Information("Refused battle state {BattleState} for {MapEventId}: stale host epoch {Stale} (current {Current})",
                            battleState, mapEventId, payload.What.HostEpoch, hostAssignment.Epoch);
                        return;
                    }
                }

                playerPartyIds = MapEventPlayerPartyCollector.CollectPartyIds(mapEvent, objectManager);
                publishConclusion = battleState == BattleState.AttackerVictory ||
                    battleState == BattleState.DefenderVictory;

                mapEvent.BattleState = battleState;
                applied = mapEvent.BattleState == battleState;
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply {Message}", nameof(NetworkChangeBattleState));
            }
            finally
            {
                applied |= mapEvent?.BattleState == battleState;
                // Victory finalization is separate from the native result commit, so run it even if a later
                // callback in the BattleState setter threw after the state itself changed.
                if (applied && publishConclusion)
                    messageBroker.Publish(this, new MapEventConcluded(mapEventId, playerPartyIds));

                messageBroker.Publish(this, new BattleStateChangeProcessed(mapEventId, battleState, applied));
            }
        });
    }

    private void Handle_MapEventSurrenderAttempted(MessagePayload<MapEventSurrenderAttempted> payload)
    {
        if (!objectManager.TryGetIdWithLogging(payload.What.MapEvent, out var mapEventId))
            return;

        network.SendAll(new NetworkMapEventSurrender(mapEventId, payload.What.Side));
    }

    private void Handle_NetworkMapEventSurrender(MessagePayload<NetworkMapEventSurrender> payload)
    {
        if (ModInformation.IsClient)
            return;

        var mapEventId = payload.What.MapEventId;
        var side = payload.What.Side;

        // Apply the surrender on the game thread with patches live, ahead of the battle-state relay
        // that follows on the same FIFO queue (the client forwards the surrender before it forwards
        // the resulting battle-state change). DoSurrender marks the defeated side as surrendered and
        // sets the victory state, so the server's capture takes the full surrendered prisoner count
        // instead of the reduced battle rate; the later battle-state relay then applies as a no-op.
        GameThread.Run(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging<MapEvent>(mapEventId, out var mapEvent))
                    return;

                // Skip if this side already surrendered — another pipeline (e.g. a PvP loser's
                // NetworkPlayerSurrendered) may have already applied it.
                if (mapEvent.GetMapEventSide(side).IsSurrendered)
                    return;

                mapEvent.DoSurrender(side);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply {Message}", nameof(NetworkMapEventSurrender));
            }
        });
    }
}
