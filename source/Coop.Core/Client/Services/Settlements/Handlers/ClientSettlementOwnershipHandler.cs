using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Settlements.Messages;
using Coop.Core.Server.Services.Settlements.Messages;
using GameInterface.Services.Settlements.Messages;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Client.Services.Settlements.Handlers
{
    /// <summary>
    /// Client handler for settlement ownership
    /// </summary>
    public class ClientSettlementOwnershipHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ClientSettlementOwnershipHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<LocalSettlementOwnershipChange>(Handle);
            messageBroker.Subscribe<SettlementOwnershipChangeApproved>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<LocalSettlementOwnershipChange>(Handle);
            messageBroker.Unsubscribe<SettlementOwnershipChangeApproved>(Handle);
        }

        private void Handle(MessagePayload<LocalSettlementOwnershipChange> obj)
        {
            var payload = obj.What;

            var message = new SettlementOwnershipChangeRequest(payload.SettlementId, payload.OwnerId, payload.CapturerId, payload.Detail);

            network.SendAll(message);
        }
        private void Handle(MessagePayload<SettlementOwnershipChangeApproved> obj)
        {
            var payload = obj.What;

            var message = new ChangeSettlementOwnership(payload.SettlementId, payload.OwnerId, payload.CapturerId, payload.Detail);

            messageBroker.Publish(this, message);
        }

    }
}
