using Common.Messaging;
using Common.Network;
using GameInterface.Services.Settlements.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Client.Services.Settlements.Handlers
{
    /// <summary>
    /// Handles <see cref="NetworkChangeSettlementComponentGold"/>, <see cref="NetworkChangeSettlementComponentIsOwnerUnassigned"/> and <see cref="NetworkChangeSettlementComponentOwner"/> from the server and then goes to the <see cref="GameInterface.Services.Settlements.Handlers.SettlementComponentHandler"/>.
    /// </summary>
    public class ClientSettlementComponentHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ClientSettlementComponentHandler(IMessageBroker messageBroker, INetwork network) 
        {
            this.messageBroker = messageBroker;
            this.network = network;
            messageBroker.Subscribe<NetworkChangeSettlementComponentGold>(HandleChangedGold);
            messageBroker.Subscribe<NetworkChangeSettlementComponentIsOwnerUnassigned>(HandleChangedIsOwnerUnassigned);
            messageBroker.Subscribe<NetworkChangeSettlementComponentOwner>(HandleChangedOwner);
        }

        private void HandleChangedOwner(MessagePayload<NetworkChangeSettlementComponentOwner> payload)
        {
            var obj = payload.What;
            var message = new ChangeSettlementComponentOwner(obj.SettlementComponentId, obj.OwnerId);
            messageBroker.Publish(this, message);
        }

        private void HandleChangedIsOwnerUnassigned(MessagePayload<NetworkChangeSettlementComponentIsOwnerUnassigned> payload)
        {
            var obj = payload.What;
            var message = new ChangeSettlementComponentIsOwnerUnassigned(obj.SettlementComponentId, obj.IsOwnerUnassigned);
            messageBroker.Publish(this, message);
        }

        private void HandleChangedGold(MessagePayload<NetworkChangeSettlementComponentGold> payload)
        {
            var obj = payload.What;
            var message = new ChangeSettlementComponentGold(obj.SettlementComponentId, obj.Gold);
            messageBroker.Publish(this, message);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkChangeSettlementComponentGold>(HandleChangedGold);
            messageBroker.Unsubscribe<NetworkChangeSettlementComponentIsOwnerUnassigned>(HandleChangedIsOwnerUnassigned);
            messageBroker.Unsubscribe<NetworkChangeSettlementComponentOwner>(HandleChangedOwner);
        }

    }
}
