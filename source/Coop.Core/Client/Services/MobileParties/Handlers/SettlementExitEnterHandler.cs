using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Client.Services.MapEvents.Handlers
{
    /// <summary>
    /// Handles changes to parties for settlement entry and exit.
    /// </summary>
    public class SettlementExitEnterHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public SettlementExitEnterHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<SettlementEntered>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<SettlementEntered>(Handle);
        }

        private void Handle(MessagePayload<SettlementEntered> obj)
        {
            network.SendAll(new SettlementEnterRequest(obj.What.StringId, obj.What.PartyId));
        }
    }
}
