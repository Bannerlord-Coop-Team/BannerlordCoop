using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.Clans.Messages;
using Coop.Core.Client.Services.Heroes.Messages;
using Coop.Core.Server.Services.Clans.Messages;
using Coop.Core.Server.Services.Heroes.Messages;
using GameInterface.Services.Clans.Messages;
using GameInterface.Services.Heroes.Messages;
using System;
using System.Collections.Generic;
using System.Text;

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

            Send(payload.PartyId, payload.CharacterId, payload.IsEventCalled);
        }

        private void Handle(MessagePayload<NetworkTakePrisonerRequest> obj)
        {
            var payload = obj.What;

            Send(payload.PartyId, payload.CharacterId, payload.IsEventCalled);
        }

        private void Send(string partyId, string characterId, bool isEventCalled)
        {
            TakePrisoner takePrisoner = new TakePrisoner(partyId, characterId, isEventCalled);

            messageBroker.Publish(this, takePrisoner);

            NetworkTakePrisonerApproved takePrisonerApproved = new NetworkTakePrisonerApproved(partyId, characterId, isEventCalled);

            network.SendAll(takePrisonerApproved);
        }
    }
}
