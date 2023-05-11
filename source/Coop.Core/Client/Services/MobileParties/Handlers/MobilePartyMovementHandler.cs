using Common.Messaging;
using Common.Network;
using Coop.Core.Common.Services.PartyMovement.Messages;
using GameInterface.Services.MobileParties.Messages;
using System;

namespace Coop.Core.Client.Services.MobileParties.Handlers
{
    public class MobilePartyMovementHandler : IHandler
    {
        private readonly INetworkMessageBroker networkMessageBroker;

        public MobilePartyMovementHandler(INetworkMessageBroker networkMessageBroker)
        {
            this.networkMessageBroker = networkMessageBroker;

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
            networkMessageBroker.PublishNetworkEvent(new NetworkUpdatePartyTargetPosition(obj));
        }

        // Incoming
        private void Handle_NetworkUpdatePartyTargetPosition(MessagePayload<NetworkUpdatePartyTargetPosition> obj)
        {
            networkMessageBroker.Publish(this, new UpdatePartyTargetPosition(Guid.Empty, obj.What.TargetPositionData));
        }
    }
}
;