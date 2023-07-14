using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Inventory.Messages;
using Coop.Core.Server.Services.Inventory.Messages;
using GameInterface.Services.Inventory.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Client.Services.Inventory.Handlers
{
    /// <summary>
    /// Handles changes to ItemRosters on the client side.
    /// </summary>
    public class ClientItemRosterHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ClientItemRosterHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<ItemRosterUpdateAttempted>(Handle);
            messageBroker.Subscribe<NetworkApproveItemRosterUpdate>(Handle);
        }
        public void Dispose()
        {
            messageBroker.Unsubscribe<ItemRosterUpdateAttempted>(Handle);
            messageBroker.Unsubscribe<NetworkApproveItemRosterUpdate>(Handle);
        }

        private void Handle(MessagePayload<ItemRosterUpdateAttempted> obj)
        {
            var payload = obj.What;

            //var message = new NetworkRequestItemRosterUpdate(payload.ItemId, payload.ModifierId, payload.Amount, payload.PartyId);

            //network.SendAll(message);
        }
        private void Handle(MessagePayload<NetworkApproveItemRosterUpdate> obj)
        {
            var payload = obj.What;

            var message = new ItemRosterUpdated(payload.ItemId, payload.ModifierId, payload.Amount, payload.PartyId);

            messageBroker.Publish(this, message);
        }
    }
}
