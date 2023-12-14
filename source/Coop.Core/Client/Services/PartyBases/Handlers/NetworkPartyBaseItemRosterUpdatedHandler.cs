using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.PartyBases.Messages;
using GameInterface.Services.ItemRosters.Messages.Events;

namespace Coop.Core.Client.Services.PartyBases.Handlers
{
    public class NetworkPartyBaseItemRosterUpdatedHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public NetworkPartyBaseItemRosterUpdatedHandler(IMessageBroker broker, INetwork network)
        {
            messageBroker = broker;
            this.network = network;

            messageBroker.Subscribe<NetworkPartyBaseItemRosterUpdated>(Handle);
        }

        public void Handle(MessagePayload<NetworkPartyBaseItemRosterUpdated> payload)
        {
            messageBroker.Publish(this, new PartyBaseItemRosterUpdated(payload.What.PartyBaseId, payload.What.EquipmentElement, payload.What.Number));
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkPartyBaseItemRosterUpdated>(Handle);
        }
    }
}
