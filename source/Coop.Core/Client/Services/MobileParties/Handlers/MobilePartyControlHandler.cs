using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages.Control;

namespace Coop.Core.Client.Services.MobileParties.Handlers
{
    public class MobilePartyControlHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public MobilePartyControlHandler(IMessageBroker messageBroker, INetwork network) 
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<MainPartyChanged>(Handle);
            messageBroker.Subscribe<NetworkGrantPartyControl>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<MainPartyChanged>(Handle);
            messageBroker.Unsubscribe<NetworkGrantPartyControl>(Handle);
        }

        private void Handle(MessagePayload<MainPartyChanged> obj)
        {
            network.SendAll(new NetworkRequestMobilePartyControl(obj.What.NewPartyId));
        }

        private void Handle(MessagePayload<NetworkGrantPartyControl> obj)
        {
            messageBroker.Publish(this, new UpdateMobilePartyControl(obj.What.PartyId, PartyControlAction.Grant));
        }
    }
}
