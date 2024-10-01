using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Heroes.Messages;
using Coop.Core.Server.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Messages;

namespace Coop.Core.Server.Services.Heroes.Handler
{
    /// <summary>
    /// Handles all captures of prisoners on server
    /// </summary>
    public class ServerTakePrisonerHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ServerTakePrisonerHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<PrisonerTaken>(Handle);
            messageBroker.Subscribe<NetworkTakePrisonerRequest>(Handle);
        }
        public void Dispose()
        {
            messageBroker.Unsubscribe<PrisonerTaken>(Handle);
            messageBroker.Unsubscribe<NetworkTakePrisonerRequest>(Handle);
        }
        private void Handle(MessagePayload<PrisonerTaken> obj)
        {
            var payload = obj.What;

            NetworkTakePrisonerApproved takePrisonerApproved = new NetworkTakePrisonerApproved(payload.PartyId, payload.CharacterId, payload.IsEventCalled);

            network.SendAll(takePrisonerApproved);
        }

        private void Handle(MessagePayload<NetworkTakePrisonerRequest> obj)
        {
            var payload = obj.What;

            TakePrisoner takePrisoner = new TakePrisoner(payload.PartyId, payload.CharacterId, payload.IsEventCalled);

            messageBroker.Publish(this, takePrisoner);

            NetworkTakePrisonerApproved takePrisonerApproved = new NetworkTakePrisonerApproved(payload.PartyId, payload.CharacterId, payload.IsEventCalled);

            network.SendAll(takePrisonerApproved);
        }
    }
}
