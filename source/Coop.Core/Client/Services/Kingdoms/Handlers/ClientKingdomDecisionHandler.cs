using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Kingdoms.Messages;
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

            messageBroker.Subscribe<LocalDecisionAdded>(HandleLocalDecisionAdded);
            messageBroker.Subscribe<LocalDecisionRemoved>(HandleLocalDecisionRemoved);
            messageBroker.Subscribe<AddDecisionApproved>(HandleAddDecisionApproved);
            messageBroker.Subscribe<RemoveDecisionApproved>(HandleRemoveDecisionApproved);
        }

        private void HandleRemoveDecisionApproved(MessagePayload<RemoveDecisionApproved> obj)
        {
            var payload = obj.What;
            var removeDecisionEvent = new RemoveDecision(payload.KingdomId, payload.Data);
            messageBroker.Publish(this, removeDecisionEvent);
        }

        private void HandleAddDecisionApproved(MessagePayload<AddDecisionApproved> obj)
        {
            var payload = obj.What;
            var addDecisionEvent = new AddDecision(payload.KingdomId, payload.Data, payload.IgnoreInfluenceCost);
            messageBroker.Publish(this, addDecisionEvent);
        }

        private void HandleLocalDecisionRemoved(MessagePayload<LocalDecisionRemoved> obj)
        {
            var payload = obj.What;
            var message = new RemoveDecisionRequest(payload.KingdomId, payload.Data);
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
            messageBroker.Unsubscribe<AddDecisionApproved>(HandleAddDecisionApproved);
            messageBroker.Unsubscribe<RemoveDecisionApproved>(HandleRemoveDecisionApproved);
        }
    }
}
