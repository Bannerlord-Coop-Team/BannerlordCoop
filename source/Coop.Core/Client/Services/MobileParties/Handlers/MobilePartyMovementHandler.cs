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
        private readonly IMessageBroker networkMessageBroker;
        private readonly INetwork network;

        public MobilePartyMovementHandler(IMessageBroker networkMessageBroker, INetwork network)
        {
            this.networkMessageBroker = networkMessageBroker;
            this.network = network;
            networkMessageBroker.Subscribe<ControlledPartyTargetPositionUpdated>(Handle_ControlledPartyTargetPositionUpdated);
            networkMessageBroker.Subscribe<NetworkUpdatePartyTargetPosition>(Handle_NetworkUpdatePartyTargetPosition);
        }
        public void Dispose()
        {
            networkMessageBroker.Unsubscribe<ControlledPartyTargetPositionUpdated>(Handle_ControlledPartyTargetPositionUpdated);
            networkMessageBroker.Unsubscribe<NetworkUpdatePartyTargetPosition>(Handle_NetworkUpdatePartyTargetPosition);
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
            networkMessageBroker.Publish(this, new UpdatePartyTargetPosition(obj.What.TargetPositionData));
        }
    }
}
;