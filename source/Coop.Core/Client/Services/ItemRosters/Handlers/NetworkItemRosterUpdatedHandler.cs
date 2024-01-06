using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.PartyBases.Messages;
using GameInterface.Services.ItemRosters.Messages.Events;

namespace Coop.Core.Client.Services.PartyBases.Handlers
{
    public class NetworkItemRosterUpdatedHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public NetworkItemRosterUpdatedHandler(IMessageBroker broker, INetwork network)
        {
            messageBroker = broker;
            this.network = network;

            messageBroker.Subscribe<NetworkItemRosterUpdate>(Handle);
        }

        public void Handle(MessagePayload<NetworkItemRosterUpdate> payload)
        {
            messageBroker.Publish(this, new ItemRosterUpdate(
                    payload.What.PartyBaseID,
                    payload.What.ItemID,
                    payload.What.ItemModifierID,
                    payload.What.Amount)
                );
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkItemRosterUpdate>(Handle);
        }
    }
}
