using Common.Messaging;
using Common.Network;
using Coop.Core.Client.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents;

namespace Coop.Core.Client.Services.MapEvents.Handlers
{
    /// <summary>
    /// Handles changes to parties for settlement entry and exit.
    /// </summary>
    public class SettlementExitEnterHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public SettlementExitEnterHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<SettlementEntered>(Handle);
            messageBroker.Subscribe<SettlementLeft>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<SettlementEntered>(Handle);
            messageBroker.Unsubscribe<SettlementLeft>(Handle);
        }

        private void Handle(MessagePayload<SettlementEntered> obj)
        {
            network.SendAll(new SettlementEnterRequest(obj.What.StringId, obj.What.PartyId));
        }

        private void Handle(MessagePayload<SettlementLeft> obj)
        {
            network.SendAll(new SettlementLeaveRequest(obj.What.StringId, obj.What.PartyId));
        }
    }
}
