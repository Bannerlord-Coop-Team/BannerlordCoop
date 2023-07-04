using Common.Logging;
using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MapEvents.Messages;
using Coop.Core.Server.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents;
using GameInterface.Services.ObjectManager;
using LiteNetLib;
using Serilog;

namespace Coop.Core.Server.Services.MapEvents
{
    /// <summary>
    /// Handles changes to parties for settlement entry and exit.
    /// </summary>
    public class SettlementExitEnterHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;
        private readonly ILogger Logger = LogManager.GetLogger<SettlementExitEnterHandler>();

        public SettlementExitEnterHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;
            messageBroker.Subscribe<SettlementEnterRequest>(Handle);
            messageBroker.Subscribe<SettlementLeaveRequest>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<SettlementLeaveRequest>(Handle);
        }

        private void Handle(MessagePayload<SettlementEnterRequest> obj)
        {
            PartyEnteredSettlement partyEnteredSettlement = new PartyEnteredSettlement(obj.What.StringId, obj.What.PartyId);

            network.Send(obj.Who as NetPeer, new SettlementEnterAllowed(obj.What.StringId, obj.What.PartyId));

            network.SendAllBut(obj.Who as NetPeer, partyEnteredSettlement);

            messageBroker.Publish(this, partyEnteredSettlement);
        }

        private void Handle(MessagePayload<SettlementLeaveRequest> obj)
        {
            var request = obj.What;

            PartyLeftSettlement partyLeftSettlement = new PartyLeftSettlement(request.StringId, request.PartyId);

            network.Send(obj.Who as NetPeer, new SettlementLeaveAllowed(request.StringId, request.PartyId));

            network.SendAllBut(obj.Who as NetPeer, partyLeftSettlement);

            messageBroker.Publish(this, partyLeftSettlement);
        }
    }
}
