using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ItemRosters.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.ItemRosters.Handlers
{
    internal class ItemRosterLifetimeHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<ItemRosterLifetimeHandler>();

        public ItemRosterLifetimeHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;

            messageBroker.Subscribe<ItemRosterCreated>(Handle);
            messageBroker.Subscribe<NetworkCreateItemRoster>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ItemRosterCreated>(Handle);
            messageBroker.Unsubscribe<NetworkCreateItemRoster>(Handle);
        }

        private void Handle(MessagePayload<ItemRosterCreated> payload)
        {
            if (objectManager.AddNewObject(payload.What.ItemRoster, out string newId) == false)
            {
                Logger.Error("Failed to add {type} to manager", typeof(ItemRoster));
                return;
            }

            network.SendAll(new NetworkCreateItemRoster(newId));
        }

        private void Handle(MessagePayload<NetworkCreateItemRoster> payload)
        {
            var newItemRotster = ObjectHelper.SkipConstructor<ItemRoster>();

            var data = payload.What;

            if (objectManager.AddExisting(data.RosterId, newItemRotster) == false)
            {
                Logger.Error("Failed to add {type} to manager with id {id}", typeof(ItemRoster), data.RosterId);
                return;
            }
        }
    }
}
