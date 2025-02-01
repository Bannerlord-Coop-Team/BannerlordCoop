using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ItemObjects.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.Core;

namespace GameInterface.Services.ItemObjects.Handlers
{
    internal class ItemObjectLifetimeHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<ItemObjectLifetimeHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;

        public ItemObjectLifetimeHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<ItemObjectCreated>(Handle_ItemObjectCreated);
            messageBroker.Subscribe<NetworkCreateItemObject>(Handle_CreateItemObject);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ItemObjectCreated>(Handle_ItemObjectCreated);
            messageBroker.Unsubscribe<NetworkCreateItemObject>(Handle_CreateItemObject);
        }

        private void Handle_ItemObjectCreated(MessagePayload<ItemObjectCreated> obj)
        {
            var payload = obj.What;

            if (objectManager.AddNewObject(payload.ItemObject, out string itemObjectId) == false) return;

            var message = new NetworkCreateItemObject(itemObjectId);
            network.SendAll(message);
        }

        private void Handle_CreateItemObject(MessagePayload<NetworkCreateItemObject> obj)
        {
            var payload = obj.What;

            var itemObject = ObjectHelper.SkipConstructor<ItemObject>();
            if (objectManager.AddExisting(payload.ItemObjectId, itemObject) == false)
            {
                Logger.Error("Failed to add existing Building, {id}", payload.ItemObjectId);
                return;
            }
        }
    }
}
