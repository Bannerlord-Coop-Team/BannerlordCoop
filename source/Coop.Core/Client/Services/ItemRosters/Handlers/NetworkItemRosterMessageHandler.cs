using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.ItemRosters.Messages;
using GameInterface.Services.ItemRosters.Messages;

namespace Coop.Core.Client.Services.PartyBases.Handlers
{
    /// <summary>
    /// Handles NetworkItemRosterUpdate and publishes UpdateItemRoster
    /// </summary>
    public class NetworkItemRosterMessageHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;


        public NetworkItemRosterMessageHandler(IMessageBroker broker)
        {
            messageBroker = broker;

            messageBroker.Subscribe<NetworkItemRosterUpdate>(Handle);
            messageBroker.Subscribe<NetworkItemRosterClear>(Handle);
        }

        public void Handle(MessagePayload<NetworkItemRosterUpdate> payload)
        {
            messageBroker.Publish(this, new UpdateItemRoster(
                    payload.What.PartyBaseID,
                    payload.What.ItemID,
                    payload.What.ItemModifierID,
                    payload.What.Amount)
                );
        }

        public void Handle(MessagePayload<NetworkItemRosterClear> payload)
        {
            messageBroker.Publish(this, new ClearItemRoster(payload.What.PartyBaseID));
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkItemRosterUpdate>(Handle);
            messageBroker.Unsubscribe<NetworkItemRosterClear>(Handle);
        }
    }
}
