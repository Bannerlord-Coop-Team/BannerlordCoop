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
    }



    public void Dispose()
    {
        messageBroker.Unsubscribe<MapEventCreated>(Handle);
        messageBroker.Unsubscribe<NetworkCreateMapEvent>(Handle);

        messageBroker.Unsubscribe<MapEventSidesArrayUpdated>(Handle);
        messageBroker.Unsubscribe<NetworkUpdateMapSidesArray>(Handle);

        messageBroker.Unsubscribe<MapEventDestroyed>(Handle);
        messageBroker.Unsubscribe<NetworkDestroyMapEvent>(Handle);
    }

    private void Handle(MessagePayload<MapEventCreated> payload)
    {
        objectManager.AddNewObject(payload.What.Instance, out var id);

        network.SendAll(new NetworkCreateMapEvent(id));
    }


    private void Handle(MessagePayload<NetworkCreateMapEvent> payload)
    {
        var newMapEvent = new MapEvent();

        newMapEvent._sides = new MapEventSide[2];
        newMapEvent.StrengthOfSide = new float[2];
        newMapEvent.MapEventVisual = Campaign.Current.VisualCreator.CreateMapEventVisual(newMapEvent);

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
                mapEvent.FinalizeEvent();
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
