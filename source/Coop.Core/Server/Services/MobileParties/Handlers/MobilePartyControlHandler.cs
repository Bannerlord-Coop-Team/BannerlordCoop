using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages;

namespace Coop.Core.Server.Services.MobileParties.Handlers
{
    public class MobilePartyControlHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public MobilePartyControlHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<NetworkRequestMobilePartyControl>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkRequestMobilePartyControl>(Handle);
        }

        private void Handle(MessagePayload<NetworkRequestMobilePartyControl> obj)
        {
            string partyId = obj.What.PartyId;

            messageBroker.Publish(this, new UpdateMobilePartyControl(partyId, PartyControlAction.Revoke));

            network.SendAll(new NetworkGrantPartyControl(partyId));
        }
    }
}
