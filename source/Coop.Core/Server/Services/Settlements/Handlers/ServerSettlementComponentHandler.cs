using Common.Messaging;
using Common.Network;
using GameInterface.Services.Settlements.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Server.Services.Settlements.Handlers
{
    public class ServerSettlementComponentHandler : IHandler
    {
        private IMessageBroker messageBroker;
        private INetwork network;

        public ServerSettlementComponentHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            messageBroker.Subscribe<SettlementComponentChangedGold>(ChangedGold);
            messageBroker.Subscribe<SettlementComponentChangedIsOwnerUnassigned>(ChangedIsOwnerUnassigned);
            messageBroker.Subscribe<SettlementComponentChangedOwner>(ChangedOwner);
        }
        public void Dispose()
        {
            messageBroker.Unsubscribe<SettlementComponentChangedGold>(ChangedGold);
            messageBroker.Unsubscribe<SettlementComponentChangedIsOwnerUnassigned>(ChangedIsOwnerUnassigned);
            messageBroker.Unsubscribe<SettlementComponentChangedOwner>(ChangedOwner);
        }

        private void ChangedOwner(MessagePayload<SettlementComponentChangedOwner> payload)
        {
            var obj = payload.What;
            network.SendAll(new NetworkSettlementComponentChangedOwner(obj.SettlementComponentId, obj.OwnerId));
        }

        private void ChangedIsOwnerUnassigned(MessagePayload<SettlementComponentChangedIsOwnerUnassigned> payload)
        {
            var obj = payload.What;
            network.SendAll(new NetworkSettlementComponentChangedIsOwnerUnassigned(obj.SettlementComponentId, obj.IsOwnerUnassigned));
        }

        private void ChangedGold(MessagePayload<SettlementComponentChangedGold> payload)
        {
            var obj = payload.What;
            network.SendAll(new NetworkSettlementComponentChangedGold(obj.SettlementComponentId, obj.Gold));
        }

    }
}
