using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Handlers;

internal class MapEventHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapEventHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    public MapEventHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;

        messageBroker.Subscribe<MapEventSidesArrayUpdated>(Handle);
        messageBroker.Subscribe<NetworkUpdateMapSidesArray>(Handle);

        messageBroker.Subscribe<MapEventInitialize>(Handle);
        messageBroker.Subscribe<NetworkMapEventInitialize>(Handle);

        messageBroker.Subscribe<LeaveBattleAttempted>(Handle);
        messageBroker.Subscribe<NetworkLeaveBattle>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<MapEventSidesArrayUpdated>(Handle);
        messageBroker.Unsubscribe<NetworkUpdateMapSidesArray>(Handle);

        messageBroker.Unsubscribe<MapEventInitialize>(Handle);
        messageBroker.Unsubscribe<NetworkMapEventInitialize>(Handle);

        messageBroker.Unsubscribe<LeaveBattleAttempted>(Handle);
        messageBroker.Unsubscribe<NetworkLeaveBattle>(Handle);
    }

    private void Handle(MessagePayload<NetworkMapEventInitialize> payload)
    {
        if (objectManager.TryGetObject<MapEvent>(payload.What.MapEventId, out var mapEvent) == false)
        {
            Logger.Error("Unable to get {type} if from {obj}", nameof(MapEvent), payload.What.MapEventId);
            return;
        }
        if (objectManager.TryGetObject<PartyBase>(payload.What.AttackerPartyId, out var attackerParty) == false)
        {
            Logger.Error("Unable to get {type} if from {obj}", nameof(PartyBase), payload.What.AttackerPartyId);
            return;
        }
        if (objectManager.TryGetObject<PartyBase>(payload.What.DefenderPartyId, out var defenderParty) == false)
        {
            Logger.Error("Unable to get {type} if from {obj}", nameof(PartyBase), payload.What.DefenderPartyId);
            return;
        }

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

                mapEvent.MapEventVisual.Initialize(mapEvent.Position, mapEvent.GetBattleSizeValue(), mapEvent.IsVisible);
                mapEvent.Component.InitializeComponent();

                //mapEvent.Initialize(attackerParty, defenderParty, component, (MapEvent.BattleTypes)payload.What.BattleType);
            }
        });
    }

    private void Handle(MessagePayload<MapEventInitialize> payload)
    {
        var obj = payload.What;

        if (objectManager.TryGetId(obj.MapEvent, out var mapEventId) == false)
        {
            Logger.Error("Unable to get {type} id from {obj}", nameof(MapEvent), mapEventId);
            return;
        }

        if (objectManager.TryGetId(obj.AttackerParty, out var attackerPartyId) == false)
        {
            Logger.Error("Unable to get {type} id from {obj}", nameof(PartyBase), attackerPartyId);
            return;
        }


        if (objectManager.TryGetId(obj.DefenderParty, out var defenderPartyId) == false)
        {
            Logger.Error("Unable to get {type} id from {obj}", nameof(PartyBase), defenderPartyId);
            return;
        }

        network.SendAll(new NetworkMapEventInitialize(mapEventId, (int)obj.BattleType, attackerPartyId, defenderPartyId));
    }

    //private void Handle(MessagePayload<MapEventCreated> payload)
    //{
    //    objectManager.AddNewObject(payload.What.Instance, out var id);

    //    network.SendAll(new NetworkCreateMapEvent(id));
    //}


    //private void Handle(MessagePayload<NetworkCreateMapEvent> payload)
    //{
    //    using (new AllowedThread())
    //    {
    //        MapEvent mapEvent = new MapEvent
    //        {
    //            StringId = payload.What.MapEventId
    //        };

    //        objectManager.AddExisting(payload.What.MapEventId, mapEvent);
    //    }
    //}

    //private void Handle(MessagePayload<MapEventDestroyed> payload)
    //{
    //    var mapEvent = payload.What.Instance;
    //    if (objectManager.TryGetId(mapEvent, out var mapEventId) == false)
    //    {
    //        Logger.Error("Unable to get {type} if from {obj}", nameof(MapEvent), mapEventId);
    //        return;
    //    }

    //    objectManager.Remove(payload.What.Instance);

    //    network.SendAll(new NetworkDestroyMapEvent(mapEventId));
    //}

    //private void Handle(MessagePayload<NetworkDestroyMapEvent> payload)
    //{
    //    if (objectManager.TryGetObject<MapEvent>(payload.What.MapEventId, out var mapEvent) == false)
    //    {
    //        Logger.Error("Unable to get {type} if from {obj}", nameof(MapEvent), payload.What.MapEventId);
    //        return;
    //    }

    //    objectManager.Remove(mapEvent);

    //    GameLoopRunner.RunOnMainThread(() =>
    //    {
    //        using (new AllowedThread())
    //        {
    //            mapEvent.Component?.FinishComponent();
    //            mapEvent.FinalizeEventAux();
    //        }
    //    });
    //}

    private void Handle(MessagePayload<LeaveBattleAttempted> payload)
    {
        var what = payload.What;
        if (!objectManager.TryGetId(what.MobileParty, out var mobilePartyId))
        {
            return;
        }

        if (!objectManager.TryGetId(what.MapEvent, out var mapEventId))
        {
            return;
        }

        network.SendAll(new NetworkLeaveBattle(mobilePartyId, mapEventId));
    }

    private void Handle(MessagePayload<NetworkLeaveBattle> payload)
    {
        var what = payload.What;
        if (!objectManager.TryGetObject<MapEvent>(what.MapEventId, out var mapEvent))
        {
            return;
        }

        using (new AllowedThread())
        {
            mapEvent.FinalizeEvent();
        }
    }

    private void Handle(MessagePayload<MapEventSidesArrayUpdated> payload)
    {
        var mapEvent = payload.What.Instance;
        if (objectManager.TryGetId(mapEvent, out var instanceId) == false)
        {
            Logger.Error("Unable to get {type} if from {obj}", nameof(MapEvent), mapEvent);
            return;
        }

        var value = payload.What.Value;
        if (objectManager.TryGetId(value, out var valueId) == false)
        {
            Logger.Error("Unable to get {type} if from {obj}", nameof(MapEventSide), value);
            return;
        }


        network.SendAll(new NetworkUpdateMapSidesArray(instanceId, valueId, payload.What.Index));
    }

    private void Handle(MessagePayload<NetworkUpdateMapSidesArray> payload)
    {
        var instanceId = payload.What.InstanceId;
        var valueId = payload.What.ValueId;
        var index = payload.What.Index;

        if (objectManager.TryGetObject<MapEvent>(instanceId, out var mapEvent) == false)
        {
            Logger.Error("Unable to get {type} if from {obj}", nameof(MapEvent), instanceId);
            return;
        }

        if (objectManager.TryGetObject<MapEventSide>(valueId, out var mapEventSide) == false)
        {
            Logger.Error("Unable to get {type} if from {obj}", nameof(MapEventSide), valueId);
            return;
        }

        mapEvent._sides[index] = mapEventSide;
    }
}
