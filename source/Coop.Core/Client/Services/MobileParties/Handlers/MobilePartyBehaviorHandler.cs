using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Common.Services.PartyMovement.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Coop.Core.Client.Services.MobileParties.Handlers
{
    public class MobilePartyBehaviorHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public MobilePartyBehaviorHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            messageBroker.Subscribe<ControlledPartyAiBehaviorUpdated>(Handle);
            messageBroker.Subscribe<NetworkUpdatePartyAiBehavior>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ControlledPartyAiBehaviorUpdated>(Handle);
            messageBroker.Unsubscribe<NetworkUpdatePartyAiBehavior>(Handle);
        }

        private void Handle(MessagePayload<ControlledPartyAiBehaviorUpdated> obj)
        {
            network.SendAll(new NetworkRequestMobilePartyAiBehavior(obj.What.BehaviorUpdateData));
        }

        private void Handle(MessagePayload<NetworkUpdatePartyAiBehavior> obj)
        {
            messageBroker.Publish(this, new UpdatePartyAiBehavior(obj.What.BehaviorUpdateData));
        }
    }
}
