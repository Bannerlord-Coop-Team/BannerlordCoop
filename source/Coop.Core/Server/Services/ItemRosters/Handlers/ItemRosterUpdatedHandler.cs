using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.PartyBases.Messages;
using GameInterface.Services.ItemRosters.Messages;

namespace Coop.Core.Server.Services.PartyBases.Handlers
{
    /// <summary>
    /// Handles ItemRosterUpdated and sends network event to all clients.
    /// </summary>
    public class ItemRosterUpdatedHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ItemRosterUpdatedHandler(IMessageBroker broker, INetwork network)
        {
            messageBroker = broker;
            this.network = network;

            messageBroker.Subscribe<ItemRosterUpdated>(Handle);
        }

        public void Handle(MessagePayload<ItemRosterUpdated> payload)
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
            messageBroker.Unsubscribe<ItemRosterUpdated>(Handle);
        }
    }
}
