using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Inventory.Messages;
using Coop.Core.Server.Services.Inventory.Messages;

namespace Coop.Core.Server.Services.Inventory.Handlers
{
    /// <summary>
    /// Handles changes to ItemRosters on the server side.
    /// </summary>
    public class ServerItemRosterHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ServerItemRosterHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<NetworkRequestItemRosterUpdate>(Handle);
        }
        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkRequestItemRosterUpdate>(Handle);
        }
        private void Handle(MessagePayload<NetworkRequestItemRosterUpdate> obj)
        {
            var payload = obj.What;

            var message = new ItemRosterUpdated(payload.ItemId, payload.ModifierId, payload.Amount, payload.PartyId);

            messageBroker.Publish(this, message);

            var networkMessage = new NetworkApproveItemRosterUpdate(payload.ItemId, payload.ModifierId, payload.Amount, payload.PartyId);

            network.SendAll(networkMessage);
        }
    }
}