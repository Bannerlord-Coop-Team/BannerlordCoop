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

namespace Coop.Core.Server.Services.Settlements.Handlers
{
    /// <summary>
    /// Server handler for settlements
    /// </summary>
    public class ServerSettlementHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ServerSettlementHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<SettlementOwnershipChangeRequest>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<SettlementOwnershipChangeRequest>(Handle);
        }

        private void Handle(MessagePayload<SettlementOwnershipChangeRequest> obj)
        {
            var payload = obj.What;

            var message = new ChangeSettlementOwnership(payload.SettlementId, payload.OwnerId, payload.CapturerId, payload.Detail);

            messageBroker.Publish(this, message);

            var networkMessage = new SettlementOwnershipChangeApproved(payload.SettlementId, payload.OwnerId, payload.CapturerId, payload.Detail);

            network.SendAll(networkMessage);
        }
    }
}
