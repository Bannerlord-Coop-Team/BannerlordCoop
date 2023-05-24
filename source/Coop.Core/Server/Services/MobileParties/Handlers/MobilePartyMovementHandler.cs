using Common.Messaging;
using Common.Network;
using Coop.Core.Common.Services.PartyMovement.Messages;
using GameInterface.Services.MobileParties.Messages;
using LiteNetLib;
using System;
using System.Linq;

namespace Coop.Core.Server.Services.MobileParties.Handlers
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
            network.SendAll(new NetworkUpdatePartyTargetPosition(obj));
        }

        // Incoming
        private void Handle_NetworkUpdatePartyTargetPosition(MessagePayload<NetworkUpdatePartyTargetPosition> obj)
        {
            NetPeer peer = obj.Who as NetPeer;

            var targetData = obj.What.TargetPositionData;

            network.SendAllBut(peer, new NetworkUpdatePartyTargetPosition(targetData));

            messageBroker.Publish(this, new UpdatePartyTargetPosition(targetData));
        }
    }
}
