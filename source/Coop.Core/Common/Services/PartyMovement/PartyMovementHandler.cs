using Common.Messaging;
using Common.Network;
using Coop.Core.Common.Services.PartyMovement.Messages;
using GameInterface.Services.MobileParties.Messages;
using System;

namespace Coop.Core.Common.Services.PartyMovement
{
    internal interface IPartyMovementHandler : IDisposable
    {
    }

    internal class PartyMovementHandler : IPartyMovementHandler
    {
        private readonly INetworkMessageBroker _messageBroker;

        public PartyMovementHandler(INetworkMessageBroker messageBroker)
        {
            _messageBroker = messageBroker;

            _messageBroker.Subscribe<ControlledPartyTargetPositionUpdated>(HandlePartyMovement);
            _messageBroker.Subscribe<NetworkUpdatePartyTargetPosition>(HandleNetworkPartyMovement);
        }

        public void Dispose()
        {
            _messageBroker.Unsubscribe<ControlledPartyTargetPositionUpdated>(HandlePartyMovement);
        }

        private void HandlePartyMovement(MessagePayload<ControlledPartyTargetPositionUpdated> obj)
        {
            _messageBroker.PublishNetworkEvent(new NetworkUpdatePartyTargetPosition(obj));
        }

        private void HandleNetworkPartyMovement(MessagePayload<NetworkUpdatePartyTargetPosition> obj)
        {
            _messageBroker.Publish(this, new UpdatePartyTargetPosition(obj.What.TargetPositionData));
        }
    }
}
