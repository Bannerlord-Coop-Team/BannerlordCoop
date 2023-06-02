using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages;

namespace Coop.Core.Server.Services.MobileParties.Handlers
{
    public class MobilePartyBehaviorHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public MobilePartyBehaviorHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<NetworkRequestMobilePartyAiBehavior>(Handle);
            messageBroker.Subscribe<ControlledPartyAiBehaviorUpdated>(Handle);
        }
        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkRequestMobilePartyAiBehavior>(Handle);
            messageBroker.Unsubscribe<ControlledPartyAiBehaviorUpdated>(Handle);
        }

        private void Handle(MessagePayload<NetworkRequestMobilePartyAiBehavior> obj)
        {
            var data = obj.What.BehaviorUpdateData;

            network.SendAll(new NetworkUpdatePartyAiBehavior(data));

            messageBroker.Publish(this, new UpdatePartyAiBehavior(data));
        }

        private void Handle(MessagePayload<ControlledPartyAiBehaviorUpdated> obj)
        {           
            var data = obj.What.BehaviorUpdateData;

            network.SendAll(new NetworkUpdatePartyAiBehavior(data));

            messageBroker.Publish(this, new UpdatePartyAiBehavior(data));
        }
    }
}
