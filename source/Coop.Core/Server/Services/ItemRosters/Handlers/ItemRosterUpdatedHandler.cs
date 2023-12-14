using Common.Messaging;
using GameInterface.Services.ItemRosters.Messages.Events;
using Common.Network;
using Coop.Core.Server.Services.PartyBases.Messages;

namespace Coop.Core.Server.Services.PartyBases.Handlers
{
    /// <summary>
    /// Handles ItemRosterUpdated and sends network event to all clients.
    /// </summary>
    public class ItemRosterUpdatedHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ItemRosterUpdatedHandler(IMessageBroker broker, INetwork network)
        {
            messageBroker = broker;
            this.network = network;

            messageBroker.Subscribe<ItemRosterUpdated>(Handle);
        }

        public void Handle(MessagePayload<ItemRosterUpdated> payload)
        {
            network.SendAll(new NetworkItemRosterUpdated(payload.What.PartyBaseId, payload.What.EquipmentElement, payload.What.Number));
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<ItemRosterUpdated>(Handle);
        }
    }
}
