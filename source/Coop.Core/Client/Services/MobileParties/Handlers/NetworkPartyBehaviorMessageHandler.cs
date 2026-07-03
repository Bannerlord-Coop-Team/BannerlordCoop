using Common.Messaging;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages.Behavior;

namespace Coop.Core.Client.Services.MobileParties.Handlers
{
    /// <summary>
    /// Handles NetworkUpdatePartyBehavior and publishes UpdatePartyBehavior
    /// </summary>
    public class NetworkPartyBehaviorMessageHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;

        public NetworkPartyBehaviorMessageHandler(IMessageBroker broker)
        {
            messageBroker = broker;

            messageBroker.Subscribe<NetworkUpdatePartyBehavior>(Handle);
        }

        public void Handle(MessagePayload<NetworkUpdatePartyBehavior> payload)
        {
            var data = payload.What.BehaviorUpdateData;

            messageBroker.Publish(this, new UpdatePartyBehavior(ref data));
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkUpdatePartyBehavior>(Handle);
        }
    }
}
