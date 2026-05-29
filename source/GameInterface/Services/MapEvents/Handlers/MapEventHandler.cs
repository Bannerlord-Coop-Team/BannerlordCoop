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

        using (new AllowedThread())
        {
            mapEvent.BattleState = payload.What.BattleState;
        }
    }
}
