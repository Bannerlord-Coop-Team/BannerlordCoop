using Common.Messaging;
using Common.Network;
using GameInterface.Services.Settlements.Messages;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Server.Services.Settlements.Handlers
{
    /// <summary>
    /// Handles all <see cref="TaleWorlds.CampaignSystem.Settlements.SettlementComponent"/> changes
    /// </summary>
    public class ServerSettlementComponentHandler : IHandler
    {
        private IMessageBroker messageBroker;
        private INetwork network;

        public ServerSettlementComponentHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            messageBroker.Subscribe<SettlementComponentGoldChanged>(ChangedGold);
            messageBroker.Subscribe<SettlementComponentIsOwnerUnassignedChanged>(ChangedIsOwnerUnassigned);
            messageBroker.Subscribe<SettlementComponentOwnerChanged>(ChangedOwner);
        }
        public void Dispose()
        {
            messageBroker.Unsubscribe<SettlementComponentGoldChanged>(ChangedGold);
            messageBroker.Unsubscribe<SettlementComponentIsOwnerUnassignedChanged>(ChangedIsOwnerUnassigned);
            messageBroker.Unsubscribe<SettlementComponentOwnerChanged>(ChangedOwner);
        }

        private void ChangedOwner(MessagePayload<SettlementComponentOwnerChanged> payload)
        {
            var obj = payload.What;
            network.SendAll(new NetworkChangeSettlementComponentOwner(obj.SettlementComponentId, obj.OwnerId));
        }

        private void ChangedIsOwnerUnassigned(MessagePayload<SettlementComponentIsOwnerUnassignedChanged> payload)
        {
            var obj = payload.What;
            network.SendAll(new NetworkChangeSettlementComponentIsOwnerUnassigned(obj.SettlementComponentId, obj.IsOwnerUnassigned));
        }

        private void ChangedGold(MessagePayload<SettlementComponentGoldChanged> payload)
        {
            var obj = payload.What;
            network.SendAll(new NetworkChangeSettlementComponentGold(obj.SettlementComponentId, obj.Gold));
        }

    }
}
