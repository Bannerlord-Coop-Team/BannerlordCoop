using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages;
using System;

namespace Coop.Core.Client.Services.MobileParties.Handlers
{
    public class ClientPlayerSurrenderHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ClientPlayerSurrenderHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<LocalPlayerSurrendered>(Handle);
            messageBroker.Subscribe<NetworkPlayerSurrenderApproved>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<LocalPlayerSurrendered>(Handle);
            messageBroker.Unsubscribe<NetworkPlayerSurrenderApproved>(Handle);
        }

        private void Handle(MessagePayload<LocalPlayerSurrendered> obj)
        {
            var payload = obj.What;

            network.SendAll(new NetworkPlayerSurrenderRequested(payload.PlayerPartyId, payload.CaptorPartyId, payload.CharacterId));
        }
        private void Handle(MessagePayload<NetworkPlayerSurrenderApproved> obj)
        {
            var payload = obj.What;

            messageBroker.Publish(this, new SurrenderLocalPlayer(payload.CaptorPartyId));
        }
    }
}