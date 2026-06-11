using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MapEvents.Logging;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
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
    }

    public void Dispose()
    {

        messageBroker.Unsubscribe<MapEventBattleStateChangeAttempted>(Handle_MapEventBattleStateChangeAttempted);
        messageBroker.Unsubscribe<NetworkChangeBattleState>(Handle_NetworkChangeBattleState);
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
        if (!objectManager.TryGetObjectWithLogging<MapEvent>(payload.What.MapEventId, out var mapEvent))
            return;

        mapEventLogger.DebugMapEvent(mapEvent,
            "Applying network battle state change. BattleState={BattleState}",
            payload.What.BattleState);

        // Do NOT wrap in AllowedThread. Setting BattleState to a victory state runs the native setter ->
        // OnBattleWon -> CalculateAndCommitMapEventResults -> CaptureDefeatedPartyMembers, which is the
        // server-authoritative capture path. Under AllowedThread CallOriginalPolicy.IsOriginalAllowed() is
        // true, so the coop capture interceptors (PlayerStartCaptivityPatches, TakePrisonerActionPatches and
        // the PartyBelongedToAsPrisoner sync) all run the original and the capture is never replicated -
        // the client gets no capture UI and the player party is never parked. The server's BattleState
        // setter does not re-broadcast (MapEventPatches.Prefix_BattleState returns without publishing on the
        // server), so no AllowedThread is needed to prevent an echo.
        mapEvent.BattleState = payload.What.BattleState;
    }
}
