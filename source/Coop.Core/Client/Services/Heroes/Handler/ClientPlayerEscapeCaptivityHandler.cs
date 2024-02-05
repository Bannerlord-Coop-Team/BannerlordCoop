using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Heroes.Messages;
using Coop.Core.Server.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Messages;

namespace Coop.Core.Client.Services.MobileParties.Handlers
{
    public class ClientPlayerEscapeCaptivityHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ClientPlayerEscapeCaptivityHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<LocalPlayerEscaped>(Handle);
            messageBroker.Subscribe<NetworkPlayerEscapeApproved>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<LocalPlayerEscaped>(Handle);
            messageBroker.Unsubscribe<NetworkPlayerEscapeApproved>(Handle);
        }

        private void Handle(MessagePayload<LocalPlayerEscaped> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkPlayerEscapeRequest(payload.HeroId));
        }
        private void Handle(MessagePayload<NetworkPlayerEscapeApproved> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new EscapePlayer(payload.HeroId));
        }
    }
}