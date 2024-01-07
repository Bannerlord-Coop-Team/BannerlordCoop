using Common.Messaging;
using GameInterface.Services.ItemRosters.Messages.Events;
using Common.Network;
using Coop.Core.Server.Services.PartyBases.Messages;

namespace Coop.Core.Server.Services.PartyBases.Handlers
{
    /// <summary>
    /// Handles ItemRosterUpdated and sends network event to all clients.
    /// </summary>
    public class ItemRosterUpdateHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ItemRosterUpdateHandler(IMessageBroker broker, INetwork network)
        {
            messageBroker = broker;
            this.network = network;

            messageBroker.Subscribe<ItemRosterUpdate>(Handle);
        }

        public void Handle(MessagePayload<ItemRosterUpdate> payload)
        {
            network.SendAll(new NetworkItemRosterUpdate(
                    payload.What.PartyBaseID,
                    payload.What.ItemID,
                    payload.What.ItemModifierID,
                    payload.What.Amount)
                );
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ItemRosterUpdate>(Handle);
        }
    }
}
