using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Heroes.Messages;
using Coop.Core.Server.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Messages;

namespace Coop.Core.Client.Services.Heroes.Handler
{
    /// <summary>
    /// Handles all captures of prisoners on client
    /// </summary>
    public class ClientTakePrisonerHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ClientTakePrisonerHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<PrisonerTaken>(Handle);
            messageBroker.Subscribe<NetworkTakePrisonerApproved>(Handle);
        }
        public void Dispose()
        {
            messageBroker.Unsubscribe<PrisonerTaken>(Handle);
            messageBroker.Unsubscribe<NetworkTakePrisonerApproved>(Handle);
        }

        private void Handle(MessagePayload<PrisonerTaken> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkTakePrisonerRequest(payload.PartyId, payload.CharacterId, payload.IsEventCalled));
        }

        private void Handle(MessagePayload<NetworkTakePrisonerApproved> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new TakePrisoner(payload.PartyId, payload.CharacterId, payload.IsEventCalled));
        }
    }
}
