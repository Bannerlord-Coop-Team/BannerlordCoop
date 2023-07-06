using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Handlers;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Patches;
using System;
using System.Collections.Generic;
using System.Text;

namespace Coop.Core.Client.Services.MapEvents.Handlers
{
    /// <summary>
    /// Handles changes to parties for settlement entry and exit.
    /// </summary>
    public class ClientSettlementExitEnterHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ClientSettlementExitEnterHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<SettlementEntered>(Handle);
            messageBroker.Subscribe<NetworkSettlementEnter>(Handle);
            messageBroker.Subscribe<StartSettlementEncounterAttempted>(Handle);
            messageBroker.Subscribe<NetworkStartSettlementEncounter>(Handle);
        }

        

        public void Dispose()
        {
            messageBroker.Unsubscribe<SettlementEntered>(Handle);
            messageBroker.Unsubscribe<NetworkSettlementEnter>(Handle);
            messageBroker.Unsubscribe<StartSettlementEncounterAttempted>(Handle);
        }

        private void Handle(MessagePayload<SettlementEntered> obj)
        {
            network.SendAll(new NetworkSettlementEnterRequest(obj.What.SettlementId, obj.What.PartyId));
        }
        private void Handle(MessagePayload<NetworkSettlementEnter> obj)
        {
            var payload = obj.What;

            var message = new PartySettlementEnter(payload.SettlementId, payload.PartyId);
            messageBroker.Publish(this, message);
        }

        private void Handle(MessagePayload<StartSettlementEncounterAttempted> obj)
        {
            var payload = obj.What;
            var message = new NetworkRequestSettlementEncounter(payload.PartyId, payload.SettlementId);

            network.SendAll(message);
        }

        private void Handle(MessagePayload<NetworkStartSettlementEncounter> obj)
        {
            var payload = obj.What;
            var message = new StartSettlementEncounter(payload.PartyId, payload.SettlementId);

            messageBroker.Publish(this, message);
        }
    }
}
