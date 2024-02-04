using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.Heroes.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.Heroes.Messages;
using LiteNetLib;

namespace Coop.Core.Server.Services.MobileParties.Handlers
{
    public class ServerPlayerSurrenderHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ServerPlayerSurrenderHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<NetworkPlayerSurrenderRequested>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkPlayerSurrenderRequested>(Handle);
        }

        private void Handle(MessagePayload<NetworkPlayerSurrenderRequested> obj)
        {
            var payload = obj.What;

            NetPeer peer = (NetPeer)obj.Who;

            TakePrisoner takePrisoner = new TakePrisoner(payload.CaptorPartyId, payload.CharacterId, true);

            messageBroker.Publish(this, takePrisoner);

            network.SendAllBut(peer, new NetworkTakePrisonerApproved(payload.CaptorPartyId, payload.CharacterId, true));

            network.Send(peer, new NetworkPlayerSurrenderApproved(payload.CaptorPartyId));
        }
    }
}
