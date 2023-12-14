using Common.Messaging;
using GameInterface.Services.ItemRosters.Messages.Events;
using Common.Network;
using Coop.Core.Server.Services.PartyBases.Messages;

namespace Coop.Core.Server.Services.PartyBases.Handlers
{
    /// <summary>
    /// Handles PartyBaseItemRosterUpdated and sends network event to all clients.
    /// </summary>
    public class PartyBaseItemRosterUpdatedHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public PartyBaseItemRosterUpdatedHandler(IMessageBroker broker, INetwork network)
        {
            messageBroker = broker;
            this.network = network;

            messageBroker.Subscribe<PartyBaseItemRosterUpdated>(Handle);
        }

        public void Handle(MessagePayload<PartyBaseItemRosterUpdated> payload)
        {
            network.SendAll(new NetworkPartyBaseItemRosterUpdated(payload.What.PartyBaseId, payload.What.EquipmentElement, payload.What.Number));
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<PartyBaseItemRosterUpdated>(Handle);
        }
    }
}
