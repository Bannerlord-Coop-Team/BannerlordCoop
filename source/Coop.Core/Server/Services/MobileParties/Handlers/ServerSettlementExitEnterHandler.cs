using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MapEvents.Handlers;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages.Behavior;
using LiteNetLib;
using Serilog;
using System;

namespace Coop.Core.Server.Services.MobileParties.Handlers
{
    /// <summary>
    /// Handles changes to parties for settlement entry and exit.
    /// </summary>
    public class ServerSettlementExitEnterHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<ServerSettlementExitEnterHandler>();

        public ServerSettlementExitEnterHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            messageBroker.Subscribe<NetworkRequestStartSettlementEncounter>(Handle);
            messageBroker.Subscribe<NetworkRequestEndSettlementEncounter>(Handle);

            messageBroker.Subscribe<PartyEnterSettlementAttempted>(Handle);
            messageBroker.Subscribe<PartyLeaveSettlementAttempted>(Handle);
        }

        

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkRequestStartSettlementEncounter>(Handle);
            messageBroker.Unsubscribe<NetworkRequestEndSettlementEncounter>(Handle);

            messageBroker.Unsubscribe<PartyEnterSettlementAttempted>(Handle);
            messageBroker.Unsubscribe<PartyLeaveSettlementAttempted>(Handle);
        }

        private void Handle(MessagePayload<NetworkRequestStartSettlementEncounter> obj)
        {
            var payload = obj.What;
            var peer = (NetPeer)obj.Who;

            network.Send(peer, new NetworkStartSettlementEncounter(payload));

            var partyEnteredSettlement = new NetworkPartyEnterSettlement(
                payload.SettlementId, payload.PartyId);

            network.SendAllBut(peer, partyEnteredSettlement);

            var partySettlementEnter = new PartyEnterSettlement(payload.SettlementId, payload.PartyId);

            messageBroker.Publish(this, partySettlementEnter);
        }

        private void Handle(MessagePayload<NetworkRequestEndSettlementEncounter> obj)
        {
            var payload = obj.What;
            var peer = (NetPeer)obj.Who;

            network.Send(peer, new NetworkEndSettlementEncounter());

            var partyEnteredSettlement = new NetworkSettlementLeave(payload.PartyId);

            network.SendAllBut(peer, partyEnteredSettlement);

            var partySettlementEnter = new PartyLeaveSettlement(payload.PartyId);

            messageBroker.Publish(this, partySettlementEnter);
        }

        private void Handle(MessagePayload<PartyEnterSettlementAttempted> obj)
        {
            // TODO this might never happen
            var payload = obj.What;

            NetworkPartyEnterSettlement partyEnteredSettlement = new NetworkPartyEnterSettlement(payload.SettlementId, payload.PartyId);

            network.SendAll(partyEnteredSettlement);

            PartyEnterSettlement partySettlementEnter = new PartyEnterSettlement(payload.SettlementId, payload.PartyId);

            messageBroker.Publish(this, partySettlementEnter);
        }

        private void Handle(MessagePayload<PartyLeaveSettlementAttempted> obj)
        {
            var payload = obj.What;
            PartyLeaveSettlement(payload.PartyId);
        }

        private void PartyLeaveSettlement(string partyId)
        {
            var networkMessage = new NetworkPartyLeaveSettlement(partyId);

            network.SendAll(networkMessage);

            var partySettlementEnter = new PartyLeaveSettlement(partyId);

            messageBroker.Publish(this, partySettlementEnter);
        }
    }
}
