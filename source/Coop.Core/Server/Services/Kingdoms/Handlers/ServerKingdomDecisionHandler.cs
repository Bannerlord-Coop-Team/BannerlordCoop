using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Kingdoms.Messages;
using GameInterface.Services.Kingdoms.Messages;

namespace Coop.Core.Server.Services.Kingdoms.Handlers
{
    public class ServerKingdomDecisionHandler: IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ServerKingdomDecisionHandler(IMessageBroker messageBroker, INetwork network)
        { 
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<LocalDecisionAdded>(HandleLocalDecisionAdded);
            messageBroker.Subscribe<LocalDecisionRemoved>(HandleLocalDecisionRemoved);
            messageBroker.Subscribe<AddDecisionRequest>(HandleAddDecisionRequest);
            messageBroker.Subscribe<RemoveDecisionRequest>(HandleRemoveDecisionRequest);
        }

        private void HandleRemoveDecisionRequest(MessagePayload<RemoveDecisionRequest> obj)
        {
            var payload = obj.What;

            var removeDecisionEvent = new RemoveDecision(payload.KingdomId, payload.Index);
            messageBroker.Publish(this, removeDecisionEvent);
            network.SendAll(removeDecisionEvent);
        }

        private void HandleAddDecisionRequest(MessagePayload<AddDecisionRequest> obj)
        {
            var payload = obj.What;

            var addDecisionEvent = new AddDecision(payload.KingdomId, payload.Data, payload.IgnoreInfluenceCost);
            messageBroker.Publish(this, addDecisionEvent);
            network.SendAll(addDecisionEvent);
        }

        private void HandleLocalDecisionRemoved(MessagePayload<LocalDecisionRemoved> obj)
        {
            var payload = obj.What;
            var message = new RemoveDecision(payload.KingdomId, payload.Index);
            network.SendAll(message);
        }

        private void HandleLocalDecisionAdded(MessagePayload<LocalDecisionAdded> obj)
        {
            var payload = obj.What;
            var message = new AddDecision(payload.KingdomId, payload.Data, payload.IgnoreInfluenceCost);
            network.SendAll(message);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<LocalDecisionAdded>(HandleLocalDecisionAdded);
            messageBroker.Unsubscribe<LocalDecisionRemoved>(HandleLocalDecisionRemoved);
            messageBroker.Unsubscribe<AddDecisionRequest>(HandleAddDecisionRequest);
            messageBroker.Unsubscribe<RemoveDecisionRequest>(HandleRemoveDecisionRequest);
        }
    }
}
