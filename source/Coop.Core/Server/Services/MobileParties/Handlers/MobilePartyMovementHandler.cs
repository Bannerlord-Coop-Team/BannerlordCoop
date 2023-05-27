using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages;
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
            messageBroker.Subscribe<NetworkRequestMobilePartyMovement>(Handle_RequestMobilePartyMovement);
        }
        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkRequestMobilePartyMovement>(Handle_RequestMobilePartyMovement);
        }

        private void Handle_RequestMobilePartyMovement(MessagePayload<NetworkRequestMobilePartyMovement> obj)
        {
            var targetData = obj.What.TargetPositionData;

            network.SendAll(new NetworkUpdatePartyTargetPosition(targetData));

            messageBroker.Publish(this, new UpdatePartyTargetPosition(targetData));
        }
    }
}
