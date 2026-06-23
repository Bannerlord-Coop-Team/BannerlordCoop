using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MapEvents.Logging;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Handlers;

internal class MapEventHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapEventHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    private readonly IMapEventLogger mapEventLogger;

    public MapEventHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager, IMapEventLogger mapEventLogger)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        this.mapEventLogger = mapEventLogger;

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

        var message = new NetworkChangeBattleState(mapEventId, payload.What.BattleState);
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
        GameThread.Run(() =>
        {
            try
            {
                if (!objectManager.TryGetObjectWithLogging<MapEvent>(mapEventId, out var mapEvent))
                    return;

                mapEventLogger.DebugMapEvent(mapEvent,
                    "Applying network battle state change. BattleState={BattleState}",
                    battleState);

                mapEvent.BattleState = battleState;
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply {Message}", nameof(NetworkChangeBattleState));
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
        if (!ModInformation.IsServer)
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

                mapEvent.DoSurrender(side);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to apply {Message}", nameof(NetworkMapEventSurrender));
            }
        });
    }
}
