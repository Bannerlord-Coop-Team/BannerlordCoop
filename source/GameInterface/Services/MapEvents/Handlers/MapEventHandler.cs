using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using static TaleWorlds.CampaignSystem.MapEvents.MapEvent;

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
        messageBroker.Subscribe<MapEventCreated>(Handle);
        messageBroker.Subscribe<NetworkCreateMapEvent>(Handle);

        messageBroker.Subscribe<MapEventSidesArrayUpdated>(Handle);
        messageBroker.Subscribe<NetworkUpdateMapSidesArray>(Handle);

        messageBroker.Subscribe<MapEventDestroyed>(Handle);
        messageBroker.Subscribe<NetworkDestroyMapEvent>(Handle);

        messageBroker.Subscribe<MapEventInitialize>(Handle);
        messageBroker.Subscribe<NetworkMapEventInitialize>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<MapEventCreated>(Handle);
        messageBroker.Unsubscribe<NetworkCreateMapEvent>(Handle);

        messageBroker.Unsubscribe<MapEventSidesArrayUpdated>(Handle);
        messageBroker.Unsubscribe<NetworkUpdateMapSidesArray>(Handle);

        messageBroker.Unsubscribe<MapEventDestroyed>(Handle);
        messageBroker.Unsubscribe<NetworkDestroyMapEvent>(Handle);

        messageBroker.Unsubscribe<MapEventInitialize>(Handle);
        messageBroker.Unsubscribe<NetworkMapEventInitialize>(Handle);
    }

    private void Handle(MessagePayload<NetworkMapEventInitialize> payload)
    {
        if (objectManager.TryGetObject<MapEvent>(payload.What.MapEventId, out var mapEvent) == false)
        {
            Logger.Error("Unable to get {type} if from {obj}", nameof(MapEvent), payload.What.MapEventId);
            return;
        }
        using (new AllowedThread())
        {
            MapEventComponent component;

            switch ((MapEvent.BattleTypes)payload.What.BattleType)
            {
                case MapEvent.BattleTypes.FieldBattle:
                    component = new FieldBattleEventComponent(mapEvent);
                    mapEvent.Component = component;
                    break;
                case MapEvent.BattleTypes.Raid:
                    component = new RaidEventComponent(mapEvent);
                    mapEvent.Component = component;
                    break;
                case MapEvent.BattleTypes.Siege:
                    break;
                case MapEvent.BattleTypes.Hideout:
                    component = new HideoutEventComponent(mapEvent, false);
                    mapEvent.Component = component;
                    break;
                case MapEvent.BattleTypes.SallyOut:
                    break;
                case MapEvent.BattleTypes.SiegeOutside:
                    break;
                case MapEvent.BattleTypes.BlockadeSallyOutBattle:
                case MapEvent.BattleTypes.BlockadeBattle:
                    component = new BlockadeBattleMapEvent(mapEvent);
                    mapEvent.Component = component;
                    break;
            }
        }
    }

    private void Handle(MessagePayload<MapEventInitialize> payload)
    {
        var obj = payload.What;

        if (objectManager.TryGetId(obj.MapEvent, out var mapEventId) == false)
        {
            Logger.Error("Unable to get {type} if from {obj}", nameof(MapEvent), mapEventId);
            return;
        }

        network.SendAll(new NetworkMapEventInitialize(mapEventId, (int)obj.BattleType));
    }

    private void Handle(MessagePayload<MapEventCreated> payload)
    {
        objectManager.AddNewObject(payload.What.Instance, out var id);

        network.SendAll(new NetworkCreateMapEvent(id));
    }


    private void Handle(MessagePayload<NetworkCreateMapEvent> payload)
    {
        //var newMapEvent = new MapEvent();
        var newMapEvent = ObjectHelper.SkipConstructor<MapEvent>();

        // TODO find better way of doing this
        newMapEvent._sides = new MapEventSide[2];
        newMapEvent.StrengthOfSide = new float[2];

        objectManager.AddExisting(payload.What.MapEventId, newMapEvent);
    }

    private void Handle(MessagePayload<MapEventDestroyed> payload)
    {
        var mapEvent = payload.What.Instance;
        if (objectManager.TryGetId(mapEvent, out var mapEventId) == false)
        {
            Logger.Error("Unable to get {type} if from {obj}", nameof(MapEvent), mapEventId);
            return;
        }

        objectManager.Remove(payload.What.Instance);

        network.SendAll(new NetworkDestroyMapEvent(mapEventId));
    }

    private void Handle(MessagePayload<NetworkDestroyMapEvent> payload)
    {
        if (objectManager.TryGetObject<MapEvent>(payload.What.MapEventId, out var mapEvent) == false)
        {
            Logger.Error("Unable to get {type} if from {obj}", nameof(MapEvent), payload.What.MapEventId);
            return;
        }

        objectManager.Remove(mapEvent);

        GameLoopRunner.RunOnMainThread(() =>
        {
            using (new AllowedThread())
            {
                if (mapEvent._sides[0] == null) mapEvent._sides[0] = ObjectHelper.SkipConstructor(typeof(MapEventSide)) as MapEventSide;
                if (mapEvent._sides[1] == null) mapEvent._sides[1] = ObjectHelper.SkipConstructor(typeof(MapEventSide)) as MapEventSide;

                mapEvent.Component?.FinishComponent();
                mapEvent.FinalizeEventAux();
            }
        });
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
