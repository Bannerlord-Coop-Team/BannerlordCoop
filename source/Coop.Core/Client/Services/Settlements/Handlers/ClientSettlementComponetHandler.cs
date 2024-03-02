using Common.Messaging;
using Common.Network;
using GameInterface.Services.Settlements.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Client.Services.Settlements.Handlers
{
    /// <summary>
    /// Handles <see cref="NetworkSettlementComponentChangedGold"/>, <see cref="NetworkSettlementComponentChangedIsOwnerUnassigned"/> and <see cref="NetworkSettlementComponentChangedOwner"/> from the server and then goes to the <see cref="GameInterface.Services.Settlements.Handlers.SettlementComponentHandler"/>.
    /// </summary>
    public class ClientSettlementComponetHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ClientSettlementComponetHandler(IMessageBroker messageBroker, INetwork network) 
        {
            this.messageBroker = messageBroker;
            this.network = network;
            messageBroker.Subscribe<NetworkSettlementComponentChangedGold>(HandleChangedGold);
            messageBroker.Subscribe<NetworkSettlementComponentChangedIsOwnerUnassigned>(HandleChangedIsOwnerUnassigned);
            messageBroker.Subscribe<NetworkSettlementComponentChangedOwner>(HandleChangedOwner);
        }

        private void HandleChangedOwner(MessagePayload<NetworkSettlementComponentChangedOwner> payload)
        {
            var obj = payload.What;
            var message = new ChangeSettlementComponentOwner(obj.SettlementComponentId, obj.OwnerId);
            messageBroker.Publish(this, message);
        }

        private void HandleChangedIsOwnerUnassigned(MessagePayload<NetworkSettlementComponentChangedIsOwnerUnassigned> payload)
        {
            var obj = payload.What;
            var message = new ChangeSettlementComponentIsOwnerUnassigned(obj.SettlementComponentId, obj.IsOwnerUnassigned);
            messageBroker.Publish(this, message);
        }

        private void HandleChangedGold(MessagePayload<NetworkSettlementComponentChangedGold> payload)
        {
            var obj = payload.What;
            var message = new ChangeSettlementComponentGold(obj.SettlementComponentId, obj.Gold);
            messageBroker.Publish(this, message);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkSettlementComponentChangedGold>(HandleChangedGold);
            messageBroker.Unsubscribe<NetworkSettlementComponentChangedIsOwnerUnassigned>(HandleChangedIsOwnerUnassigned);
            messageBroker.Unsubscribe<NetworkSettlementComponentChangedOwner>(HandleChangedOwner);
        }

    }
}
