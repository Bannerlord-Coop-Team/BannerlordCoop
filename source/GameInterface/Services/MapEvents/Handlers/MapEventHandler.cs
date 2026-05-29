using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MapEvents.Logging;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Leave;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

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
        messageBroker.Subscribe<MapEventSidesArrayUpdated>(Handle);
        messageBroker.Subscribe<NetworkUpdateMapSidesArray>(Handle);

        messageBroker.Subscribe<MapEventInitialize>(Handle);
        messageBroker.Subscribe<NetworkMapEventInitialize>(Handle);

        messageBroker.Subscribe<LeaveBattleAttempted>(Handle);
        messageBroker.Subscribe<NetworkLeaveBattle>(Handle);

        messageBroker.Subscribe<MapEventBattleStateChangeAttempted>(Handle_MapEventBattleStateChangeAttempted);
        messageBroker.Subscribe<NetworkChangeBattleState>(Handle_NetworkChangeBattleState);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<MapEventSidesArrayUpdated>(Handle);
        messageBroker.Unsubscribe<NetworkUpdateMapSidesArray>(Handle);

        messageBroker.Unsubscribe<MapEventInitialize>(Handle);
        messageBroker.Unsubscribe<NetworkMapEventInitialize>(Handle);

        messageBroker.Unsubscribe<LeaveBattleAttempted>(Handle);
        messageBroker.Unsubscribe<NetworkLeaveBattle>(Handle);

        messageBroker.Unsubscribe<MapEventBattleStateChangeAttempted>(Handle_MapEventBattleStateChangeAttempted);
        messageBroker.Unsubscribe<NetworkChangeBattleState>(Handle_NetworkChangeBattleState);
    }

    private void Handle(MessagePayload<NetworkMapEventInitialize> payload)
    {
        if (!objectManager.TryGetObjectWithLogging<MapEvent>(payload.What.MapEventId, out var mapEvent)) return;
        if (!objectManager.TryGetObjectWithLogging<PartyBase>(payload.What.AttackerPartyId, out var attackerParty)) return;
        if (!objectManager.TryGetObjectWithLogging<PartyBase>(payload.What.DefenderPartyId, out var defenderParty)) return;

        mapEventLogger.DebugMapEvent(mapEvent,
            "Received network map event initialize. BattleType={BattleType}, AttackerPartyId={AttackerPartyId}, DefenderPartyId={DefenderPartyId}",
            payload.What.BattleType,
            payload.What.AttackerPartyId,
            payload.What.DefenderPartyId);

        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                //MapEventComponent component = null;

                //switch ((MapEvent.BattleTypes)payload.What.BattleType)
                //{
                //    case MapEvent.BattleTypes.FieldBattle:
                //        component = new FieldBattleEventComponent(mapEvent);
                //        break;
                //    case MapEvent.BattleTypes.Raid:
                //        component = new RaidEventComponent(mapEvent);
                //        break;
                //    case MapEvent.BattleTypes.Siege:
                //        break;
                //    case MapEvent.BattleTypes.Hideout:
                //        component = new HideoutEventComponent(mapEvent, false);
                //        break;
                //    case MapEvent.BattleTypes.SallyOut:
                //        break;
                //    case MapEvent.BattleTypes.SiegeOutside:
                //        break;
                //    case MapEvent.BattleTypes.BlockadeSallyOutBattle:
                //    case MapEvent.BattleTypes.BlockadeBattle:
                //        component = new BlockadeBattleMapEvent(mapEvent);
                //        break;
                //}

                mapEventLogger.DebugMapEvent(mapEvent,
                    "Initializing map event visual and component. Position={Position}, IsVisible={IsVisible}",
                    mapEvent.Position,
                    mapEvent.IsVisible);

                mapEvent.MapEventVisual.Initialize(mapEvent.Position, mapEvent.IsVisible);
                mapEvent.Component.InitializeComponent();

                //mapEvent.Initialize(attackerParty, defenderParty, component, (MapEvent.BattleTypes)payload.What.BattleType);
            }
        });
    }

    private void Handle(MessagePayload<MapEventInitialize> payload)
    {
        var obj = payload.What;

        if (!objectManager.TryGetIdWithLogging(obj.MapEvent, out var mapEventId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.AttackerParty, out var attackerPartyId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.DefenderParty, out var defenderPartyId)) return;

        mapEventLogger.DebugMapEventId(mapEventId,
            "Sending network map event initialize. BattleType={BattleType}, AttackerPartyId={AttackerPartyId}, DefenderPartyId={DefenderPartyId}",
            obj.BattleType,
            attackerPartyId,
            defenderPartyId);

        network.SendAll(new NetworkMapEventInitialize(mapEventId, (int)obj.BattleType, attackerPartyId, defenderPartyId));
    }

    private void Handle(MessagePayload<LeaveBattleAttempted> payload)
    {
        var what = payload.What;
        if (!objectManager.TryGetIdWithLogging(what.MobileParty, out var mobilePartyId)) return;
        if (!objectManager.TryGetIdWithLogging(what.MapEvent, out var mapEventId)) return;

        mapEventLogger.DebugMapEventId(mapEventId,
            "Sending network leave battle. MobilePartyId={MobilePartyId}",
            mobilePartyId);

        network.SendAll(new NetworkLeaveBattle(mobilePartyId, mapEventId));
    }

    private void Handle(MessagePayload<NetworkLeaveBattle> payload)
    {
        var what = payload.What;
        if (!objectManager.TryGetObjectWithLogging<MapEvent>(what.MapEventId, out var mapEvent)) return;

        mapEventLogger.DebugMapEvent(mapEvent,
            "Received network leave battle. MobilePartyId={MobilePartyId}",
            what.MobilePartyId);

        using (new AllowedThread())
        {
            mapEventLogger.DebugMapEvent(mapEvent, "Finalizing map event from network leave battle.");
            mapEvent.FinalizeEvent();
        }
    }

    private void Handle(MessagePayload<MapEventSidesArrayUpdated> payload)
    {
        var mapEvent = payload.What.Instance;
        if (!objectManager.TryGetIdWithLogging(mapEvent, out var instanceId)) return;

        var value = payload.What.Value;
        if (!objectManager.TryGetIdWithLogging(value, out var valueId)) return;

        mapEventLogger.DebugMapEventId(instanceId,
            "Sending network map event side array update. Index={Index}, MapEventSideId={MapEventSideId}",
            payload.What.Index,
            valueId);

        network.SendAll(new NetworkUpdateMapSidesArray(instanceId, valueId, payload.What.Index));
    }

    private void Handle(MessagePayload<NetworkUpdateMapSidesArray> payload)
    {
        var instanceId = payload.What.InstanceId;
        var valueId = payload.What.ValueId;
        var index = payload.What.Index;

        if (!objectManager.TryGetObjectWithLogging<MapEvent>(instanceId, out var mapEvent)) return;

        if (!objectManager.TryGetObjectWithLogging<MapEventSide>(valueId, out var mapEventSide)) return;

        mapEventLogger.DebugMapEvent(mapEvent,
            "Applying network map event side array update. Index={Index}, MapEventSideId={MapEventSideId}",
            index,
            valueId);

        mapEvent._sides[index] = mapEventSide;
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
