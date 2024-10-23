using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.Armies.Messages.Lifetime;
using GameInterface.Services.ItemObjects.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Core;

namespace GameInterface.Services.Buildings.Handlers
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
            messageBroker.Subscribe<ItemObjectCreated>(Handle_BuildingCreated);
            messageBroker.Subscribe<NetworkCreateItemObject>(Handle_CreateBuilding);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ItemObjectCreated>(Handle_BuildingCreated);
            messageBroker.Unsubscribe<NetworkCreateItemObject>(Handle_CreateBuilding);
        }

        private void Handle_BuildingCreated(MessagePayload<ItemObjectCreated> obj)
        {
            var payload = obj.What;

            if (objectManager.AddNewObject(payload.ItemObject, out string itemObjectId) == false) return;

            var message = new NetworkCreateItemObject(itemObjectId);
            network.SendAll(message);
        }

        private void Handle_CreateBuilding(MessagePayload<NetworkCreateItemObject> obj)
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
