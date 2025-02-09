using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.CultureObjects.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.Core;

namespace GameInterface.Services.CultureObjects.Handlers;

internal class BasicCultureObjectLifetimeHandler : IHandler
{
    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ILogger Logger = LogManager.GetLogger<BasicCultureObjectLifetimeHandler>();

    public BasicCultureObjectLifetimeHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        messageBroker.Subscribe<BasicCultureObjectCreated>(Handle);
        messageBroker.Subscribe<NetworkCreateBasicCultureObject>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<BasicCultureObjectCreated>(Handle);
        messageBroker.Unsubscribe<NetworkCreateBasicCultureObject>(Handle);
    }

    private void Handle(MessagePayload<BasicCultureObjectCreated> payload)
    {
        if (objectManager.AddNewObject(payload.What.BasicCultureObject, out string newId) == false)
        {
            Logger.Error("Failed to add {type} to manager", typeof(BasicCultureObject));
            return;
        }
        NetworkCreateBasicCultureObject message = new(newId);
        network.SendAll(message);
    }

    private void Handle(MessagePayload<NetworkCreateBasicCultureObject> obj)
    {
        var newCultureObject = ObjectHelper.SkipConstructor<BasicCultureObject>();

        var payload = obj.What;

        if (objectManager.AddExisting(payload.CultureObjectId, newCultureObject) == false)
        {
            Logger.Error("Failed to add {type} to manager with id {id}", typeof(BasicCultureObject), payload.CultureObjectId);
            return;
        }
    }
}
