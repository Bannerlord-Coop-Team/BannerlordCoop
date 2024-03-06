using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Heroes.Messages;
using Coop.Core.Server.Services.Heroes.Messages;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;

namespace Coop.Core.Server.Services.MobileParties.Handlers
{
    public class ServerPlayerEscapeHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ServerPlayerEscapeHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<NetworkPlayerEscapeRequest>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkPlayerEscapeRequest>(Handle);
        }

        private void Handle(MessagePayload<NetworkPlayerEscapeRequest> obj)
        {
            var payload = obj.What;

            NetPeer peer = (NetPeer)obj.Who;

            ReleasePrisoner releasePrisoner = new ReleasePrisoner(payload.HeroId, 4, null);

            messageBroker.Publish(this, releasePrisoner);

            network.SendAllBut(peer, new NetworkReleasePrisonerApproved(payload.HeroId, 4, null));

            network.Send(peer, new NetworkPlayerEscapeApproved(payload.HeroId));
        }
    }
}
