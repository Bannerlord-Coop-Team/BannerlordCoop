using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages;
using Coop.Core.Server.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages;
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
            messageBroker.Subscribe<NetworkSettlementEnterRequest>(Handle);
            messageBroker.Subscribe<NetworkRequestSettlementEncounter>(Handle);
        }

        

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkSettlementEnterRequest>(Handle);
        }

        private void Handle(MessagePayload<NetworkSettlementEnterRequest> obj)
        {
            var payload = obj.What;

            NetworkSettlementEnter partyEnteredSettlement = new NetworkSettlementEnter(payload.SettlementId, payload.PartyId);

            network.SendAll(partyEnteredSettlement);

            PartySettlementEnter partySettlementEnter = new PartySettlementEnter(payload.SettlementId, payload.PartyId);

            messageBroker.Publish(this, partySettlementEnter);
        }

        private void Handle(MessagePayload<NetworkRequestSettlementEncounter> obj)
        {
            var payload = obj.What;
            var peer = (NetPeer)obj.Who;

            network.Send(peer, new NetworkStartSettlementEncounter(payload));
            
            var partyEnteredSettlement = new NetworkSettlementEnter(
                payload.SettlementId, payload.PartyId);

            network.SendAllBut(peer, partyEnteredSettlement);

            var partySettlementEnter = new PartySettlementEnter(payload.SettlementId, payload.PartyId);

            messageBroker.Publish(this, partySettlementEnter);
        }
    }
}
