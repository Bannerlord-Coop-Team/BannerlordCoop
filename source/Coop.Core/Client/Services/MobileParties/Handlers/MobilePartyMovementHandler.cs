using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Common.Services.PartyMovement.Messages;
using GameInterface.Services.MobileParties.Messages;
using System;

namespace Coop.Core.Client.Services.MobileParties.Handlers
{
    public class MobilePartyMovementHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public MobilePartyMovementHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            messageBroker.Subscribe<ControlledPartyTargetPositionUpdated>(Handle_ControlledPartyTargetPositionUpdated);
            messageBroker.Subscribe<NetworkUpdatePartyTargetPosition>(Handle_NetworkUpdatePartyTargetPosition);
        }
        public void Dispose()
        {
            messageBroker.Unsubscribe<ControlledPartyTargetPositionUpdated>(Handle_ControlledPartyTargetPositionUpdated);
            messageBroker.Unsubscribe<NetworkUpdatePartyTargetPosition>(Handle_NetworkUpdatePartyTargetPosition);
        }

        // Outgoing
        private void Handle_ControlledPartyTargetPositionUpdated(MessagePayload<ControlledPartyTargetPositionUpdated> obj)
        {
            var message = new NetworkRequestMobilePartyMovement(obj.What.TargetPositionData);
            network.SendAll(message);
        }

        // Incoming
        private void Handle_NetworkUpdatePartyTargetPosition(MessagePayload<NetworkUpdatePartyTargetPosition> obj)
        {
            messageBroker.Publish(this, new UpdatePartyTargetPosition(obj.What.TargetPositionData));
        }
    }
}
;