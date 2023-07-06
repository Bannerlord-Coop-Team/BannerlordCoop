using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MobileParties.Messages;
using GameInterface.Services.MobileParties.Messages;
using LiteNetLib;
using Serilog;

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
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkSettlementEnterRequest>(Handle);
        }

        private void Handle(MessagePayload<NetworkSettlementEnterRequest> obj)
        {
            NetworkPartyEnteredSettlement partyEnteredSettlement = new NetworkPartyEnteredSettlement(obj.What.SettlementId, obj.What.PartyId);

            network.SendAllBut(obj.Who as NetPeer, partyEnteredSettlement);

            messageBroker.Publish(this, partyEnteredSettlement);
        }
    }
}
