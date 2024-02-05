using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Heroes.Messages;
using Coop.Core.Server.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Messages;

namespace Coop.Core.Server.Services.Heroes.Handler
{
    /// <summary>
    /// Handles all releases of prisoners on server
    /// </summary>
    public class ServerReleasePrisonerHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ServerReleasePrisonerHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<PrisonerReleased>(Handle);
            messageBroker.Subscribe<NetworkReleasePrisonerRequest>(Handle);
        }
        public void Dispose()
        {
            messageBroker.Unsubscribe<PrisonerReleased>(Handle);
            messageBroker.Unsubscribe<NetworkReleasePrisonerRequest>(Handle);
        }
        private void Handle(MessagePayload<PrisonerReleased> obj)
        {
            var payload = obj.What;

            NetworkReleasePrisonerApproved releasePrisonerApproved = new NetworkReleasePrisonerApproved(payload.HeroId, payload.EndCaptivityDetail, payload.FacilitatorId);

            network.SendAll(releasePrisonerApproved);
        }

        private void Handle(MessagePayload<NetworkReleasePrisonerRequest> obj)
        {
            var payload = obj.What;

            ReleasePrisoner releasePrisoner = new ReleasePrisoner(payload.HeroId, payload.EndCaptivityDetail, payload.FacilitatorId);

            messageBroker.Publish(this, releasePrisoner);

            NetworkReleasePrisonerApproved releasePrisonerApproved = new NetworkReleasePrisonerApproved(payload.HeroId, payload.EndCaptivityDetail, payload.FacilitatorId);

            network.SendAll(releasePrisonerApproved);
        }
    }
}
