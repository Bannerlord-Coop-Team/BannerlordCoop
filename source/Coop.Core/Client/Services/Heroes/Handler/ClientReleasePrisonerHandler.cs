using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Heroes.Messages;
using Coop.Core.Server.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Messages;

namespace Coop.Core.Client.Services.Heroes.Handler
{
    /// <summary>
    /// Handles all releases of prisoners on client
    /// </summary>
    public class ClientReleasePrisonerHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ClientReleasePrisonerHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<PrisonerReleased>(Handle);
            messageBroker.Subscribe<NetworkReleasePrisonerApproved>(Handle);
        }
        public void Dispose()
        {
            messageBroker.Unsubscribe<PrisonerReleased>(Handle);
            messageBroker.Unsubscribe<NetworkReleasePrisonerApproved>(Handle);
        }

        private void Handle(MessagePayload<PrisonerReleased> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkReleasePrisonerRequest(payload.HeroId, payload.EndCaptivityDetail, payload.FacilitatorId));
        }

        private void Handle(MessagePayload<NetworkReleasePrisonerApproved> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new ReleasePrisoner(payload.HeroId, payload.EndCaptivityDetail, payload.FacilitatorId));
        }
    }
}