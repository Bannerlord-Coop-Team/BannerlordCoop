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
        private readonly INetworkMessageBroker networkMessageBroker;
        private readonly ICoopServer server;

        public MobilePartyMovementHandler(INetworkMessageBroker networkMessageBroker, ICoopServer server)
        {
            this.networkMessageBroker = networkMessageBroker;
            this.server = server;
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
            NetPeer peer = obj.Who as NetPeer;

            foreach (var otherPeer in server.ConnectedPeers.Where(p => p != peer))
            {
                networkMessageBroker.PublishNetworkEvent(otherPeer, new NetworkUpdatePartyTargetPosition(obj.What.TargetPositionData));
            }

            networkMessageBroker.Publish(this, new UpdatePartyTargetPosition(Guid.Empty, obj.What.TargetPositionData));
        }
    }
}
