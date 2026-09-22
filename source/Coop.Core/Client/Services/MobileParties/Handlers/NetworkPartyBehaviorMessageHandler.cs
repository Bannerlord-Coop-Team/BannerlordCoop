using Common;
using Common.Messaging;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.MobileParties.Data;

namespace Coop.Core.Client.Services.MobileParties.Handlers
{
    /// <summary>
    /// Handles NetworkUpdatePartyBehavior and publishes UpdatePartyBehavior
    /// </summary>
    public class NetworkPartyBehaviorMessageHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly IPartyBehaviorWireMapper wireMapper;

        public NetworkPartyBehaviorMessageHandler(IMessageBroker broker, IPartyBehaviorWireMapper wireMapper)
        {
            messageBroker = broker;
            this.wireMapper = wireMapper;

            messageBroker.Subscribe<NetworkUpdatePartyBehavior>(Handle);
        }

        public void Handle(MessagePayload<NetworkUpdatePartyBehavior> payload)
        {
            GameThread.RunSafe(() =>
            {
                if (!wireMapper.TryFromNetwork(payload.What.BehaviorUpdateData, out var data)) return;

                messageBroker.Publish(this, new UpdatePartyBehavior(ref data, alreadyOnGameThread: true));
            }, context: nameof(NetworkPartyBehaviorMessageHandler));
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkUpdatePartyBehavior>(Handle);
        }
    }
}
