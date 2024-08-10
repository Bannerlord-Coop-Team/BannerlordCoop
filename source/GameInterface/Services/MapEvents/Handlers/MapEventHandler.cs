using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
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


    public MapEventHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        messageBroker.Subscribe<MapEventCreated>(Handle);
        messageBroker.Subscribe<NetworkCreateMapEvent>(Handle);

        messageBroker.Subscribe<MapEventDestroyed>(Handle);
        messageBroker.Subscribe<NetworkDestroyMapEvent>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<MapEventCreated>(Handle);
        messageBroker.Unsubscribe<NetworkCreateMapEvent>(Handle);

        messageBroker.Unsubscribe<MapEventDestroyed>(Handle);
        messageBroker.Unsubscribe<NetworkDestroyMapEvent>(Handle);
    }

    private void Handle(MessagePayload<NetworkDestroyMapEvent> payload)
    {
        if (objectManager.TryGetObject<MapEvent>(payload.What.MapEventId, out var mapEvent) == false)
        {
            Logger.Error("Unable to get {type} if from {obj}", nameof(MapEvent), payload.What.MapEventId);
            return;
        }

        objectManager.Remove(mapEvent);
    }

    private void Handle(MessagePayload<MapEventDestroyed> payload)
    {
        var mapEvent = payload.What.Instance;
        if (objectManager.TryGetId(mapEvent, out var mapEventId) == false)
        {
            Logger.Error("Unable to get {type} if from {obj}", nameof(MapEvent), mapEvent);
            return;
        }

        objectManager.Remove(payload.What.Instance);

        network.SendAll(new NetworkDestroyMapEvent(mapEventId));
    }

    

    private void Handle(MessagePayload<MapEventCreated> payload)
    {
        objectManager.AddNewObject(payload.What.Instance, out var id);

        network.SendAll(new NetworkCreateMapEvent(id));
    }


    private void Handle(MessagePayload<NetworkCreateMapEvent> payload)
    {
        var newMapEvent = ObjectHelper.SkipConstructor<MapEvent>();

        // TODO find better way of doing this
        newMapEvent._sides = new MapEventSide[2];

        objectManager.AddExisting(payload.What.MapEventId, newMapEvent);
    }
}
