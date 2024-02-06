using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Kingdoms.Messages;
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

            messageBroker.Subscribe<LocalDecisionAdded>(HandleLocalDecisionAdded);
            messageBroker.Subscribe<LocalDecisionRemoved>(HandleLocalDecisionRemoved);
        }

        private void HandleLocalDecisionRemoved(MessagePayload<LocalDecisionRemoved> obj)
        {
            var payload = obj.What;
            var message = new RemoveDecisionRequest(payload.KingdomId, payload.Index);
            network.SendAll(message);
        }

        private void HandleLocalDecisionAdded(MessagePayload<LocalDecisionAdded> obj)
        {
            var payload = obj.What;
            var message = new AddDecisionRequest(payload.KingdomId, payload.Data, payload.IgnoreInfluenceCost);
            network.SendAll(message);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<LocalDecisionAdded>(HandleLocalDecisionAdded);
            messageBroker.Unsubscribe<LocalDecisionRemoved>(HandleLocalDecisionRemoved);
        }
    }
}
