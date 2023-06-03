using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages.Behavior;

namespace Coop.Core.Client.Services.MobileParties.Handlers
{
    /// <summary>
    /// Handles client communication related to party behavior synchronisation.
    /// </summary>
    /// <seealso cref="Server.Services.MobileParties.Handlers.MobilePartyBehaviorHandler">Server's Handler</seealso>
    /// <seealso cref="GameInterface.Services.MobileParties.Handlers.MobilePartyBehaviorHandler">Game Interface's Handler</seealso>
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

        internal void Handle(MessagePayload<ControlledPartyAiBehaviorUpdated> obj)
        {
            network.SendAll(new NetworkRequestMobilePartyAiBehavior(obj.What.BehaviorUpdateData));
        }

        internal void Handle(MessagePayload<NetworkUpdatePartyAiBehavior> obj)
        {
            messageBroker.Publish(this, new UpdatePartyAiBehavior(obj.What.BehaviorUpdateData));
        }
    }
}
