using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Handlers;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages.Behavior;
using GameInterface.Services.MobileParties.Patches;

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

            messageBroker.Subscribe<StartSettlementEncounterAttempted>(Handle);
            messageBroker.Subscribe<EndSettlementEncounterAttempted>(Handle);
            messageBroker.Subscribe<NetworkEndSettlementEncounter>(Handle);
            messageBroker.Subscribe<NetworkStartSettlementEncounter>(Handle);

            messageBroker.Subscribe<NetworkPartyEnterSettlement>(Handle);
            messageBroker.Subscribe<NetworkPartyLeaveSettlement>(Handle);
        }

        

        public void Dispose()
        {
            messageBroker.Unsubscribe<StartSettlementEncounterAttempted>(Handle);
            messageBroker.Unsubscribe<EndSettlementEncounterAttempted>(Handle);
            messageBroker.Unsubscribe<NetworkEndSettlementEncounter>(Handle);
            messageBroker.Unsubscribe<NetworkStartSettlementEncounter>(Handle);

            messageBroker.Unsubscribe<NetworkPartyEnterSettlement>(Handle);
            messageBroker.Unsubscribe<NetworkPartyLeaveSettlement>(Handle);
        }
        

        private void Handle(MessagePayload<StartSettlementEncounterAttempted> obj)
        {
            var payload = obj.What;
            var message = new NetworkRequestStartSettlementEncounter(payload.PartyId, payload.SettlementId);

            network.SendAll(message);
        }

        private void Handle(MessagePayload<EndSettlementEncounterAttempted> obj)
        {
            var payload = obj.What;
            var message = new NetworkRequestEndSettlementEncounter(payload.PartyId);

            network.SendAll(message);
        }

        private void Handle(MessagePayload<NetworkStartSettlementEncounter> obj)
        {
            var payload = obj.What;
            var message = new StartSettlementEncounter(payload.PartyId, payload.SettlementId);

            messageBroker.Publish(this, message);
        }

        private void Handle(MessagePayload<NetworkEndSettlementEncounter> obj)
        {
            var message = new EndSettlementEncounter();

            messageBroker.Publish(this, message);
        }

        private void Handle(MessagePayload<NetworkPartyEnterSettlement> obj)
        {
            var payload = obj.What;

            var message = new PartyEnterSettlement(payload.SettlementId, payload.PartyId);
            messageBroker.Publish(this, message);
        }

        private void Handle(MessagePayload<NetworkPartyLeaveSettlement> obj)
        {
            var payload = obj.What;
            var message = new PartyLeaveSettlement(payload.PartyId);

            messageBroker.Publish(this, message);
        }
    }
}
