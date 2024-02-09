using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Kingdoms.Messages;
using GameInterface.Services.Kingdoms.Messages;

namespace Coop.Core.Client.Services.Kingdoms.Handlers
{
    public class ClientKingdomDecisionHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ClientKingdomDecisionHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<NetworkAddDecision>(HandleNetworkAddDecision);
            messageBroker.Subscribe<NetworkRemoveDecision>(HandleNetworkRemoveDecision);
        }

        private void HandleNetworkRemoveDecision(MessagePayload<NetworkRemoveDecision> obj)
        {
            var payload = obj.What;
            var message = new RemoveDecision(payload.KingdomId, payload.Index);
            messageBroker.Publish(this, message);
        }

        private void HandleNetworkAddDecision(MessagePayload<NetworkAddDecision> obj)
        {
            var payload = obj.What;
            var message = new AddDecision(payload.KingdomId, payload.Data, payload.IgnoreInfluenceCost);
            messageBroker.Publish(this, message);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkAddDecision>(HandleNetworkAddDecision);
            messageBroker.Unsubscribe<NetworkRemoveDecision>(HandleNetworkRemoveDecision);
        }
    }
}
