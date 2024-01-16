using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.PartyBases.Messages;
using GameInterface.Services.ItemRosters.Messages;

namespace Coop.Core.Client.Services.PartyBases.Handlers
{
    /// <summary>
    /// Handles NetworkItemRosterUpdate and publishes UpdateItemRoster
    /// </summary>
    public class NetworkItemRosterUpdateHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public NetworkItemRosterUpdateHandler(IMessageBroker broker, INetwork network)
        {
            messageBroker = broker;
            this.network = network;

            messageBroker.Subscribe<NetworkItemRosterUpdate>(Handle);
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

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkItemRosterUpdate>(Handle);
        }
    }
}
