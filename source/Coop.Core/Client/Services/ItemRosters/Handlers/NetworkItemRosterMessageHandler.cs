using Common;
using Common.Messaging;
using Coop.Core.Server.Services.ItemRosters.Messages;
using GameInterface.Services.ItemObjects;
using GameInterface.Services.ItemRosters.Messages;

namespace Coop.Core.Client.Services.ItemRosters.Handlers
{
    /// <summary>
    /// Handles NetworkItemRosterUpdate and publishes UpdateItemRoster
    /// </summary>
    public class NetworkItemRosterMessageHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly ItemObjectRegistry itemObjectRegistry;


        public NetworkItemRosterMessageHandler(
            IMessageBroker broker,
            ItemObjectRegistry itemObjectRegistry)
        {
            messageBroker = broker;
            this.itemObjectRegistry = itemObjectRegistry;

            messageBroker.Subscribe<NetworkRegisterItemHandle>(Handle);
            messageBroker.Subscribe<NetworkItemRosterUpdate>(Handle);
            messageBroker.Subscribe<NetworkItemRosterClear>(Handle);
        }

        public void Handle(MessagePayload<NetworkRegisterItemHandle> payload)
        {
            GameThread.RunSafe(() =>
                itemObjectRegistry.TryRegisterExistingItem(payload.What.StringId, payload.What.Handle));
        }

        public void Handle(MessagePayload<NetworkItemRosterUpdate> payload)
        {
            messageBroker.Publish(this, new UpdateItemRoster(
                    payload.What.ItemRosterId,
                    payload.What.ItemID,
                    payload.What.ItemModifierID,
                    payload.What.Amount)
                );
        }

        public void Handle(MessagePayload<NetworkItemRosterClear> payload)
        {
            messageBroker.Publish(this, new ClearItemRoster(payload.What.ItemRosterId));
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkRegisterItemHandle>(Handle);
            messageBroker.Unsubscribe<NetworkItemRosterUpdate>(Handle);
            messageBroker.Unsubscribe<NetworkItemRosterClear>(Handle);
        }
    }
}
