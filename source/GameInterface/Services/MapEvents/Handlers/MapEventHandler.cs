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
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<MapEventCreated>(Handle);
        messageBroker.Unsubscribe<NetworkCreateMapEvent>(Handle);
    }

    private void Handle(MessagePayload<MapEventCreated> payload)
    {
        objectManager.AddNewObject(payload.What.Instance, out var id);

        network.SendAll(new NetworkCreateMapEvent(id));
    }


    private void Handle(MessagePayload<NetworkCreateMapEvent> payload)
    {
        var newMapEvent = ObjectHelper.SkipConstructor<MapEvent>();

        objectManager.AddExisting(payload.What.MapEventId, newMapEvent);
    }
}
