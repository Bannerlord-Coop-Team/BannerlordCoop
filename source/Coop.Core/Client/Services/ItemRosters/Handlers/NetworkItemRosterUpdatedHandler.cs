using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.PartyBases.Messages;
using GameInterface.Services.ItemRosters.Messages.Events;
using System.Data;

namespace Coop.Core.Client.Services.PartyBases.Handlers
{
    public class NetworkItemRosterUpdatedHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public NetworkItemRosterUpdatedHandler(IMessageBroker broker, INetwork network)
        {
            messageBroker = broker;
            this.network = network;

            messageBroker.Subscribe<NetworkItemRosterUpdated>(Handle);
        }

        public void Handle(MessagePayload<NetworkItemRosterUpdated> payload)
        {
            messageBroker.Publish(this, new ItemRosterUpdated(payload.What.PartyBaseId, payload.What.EquipmentElement, payload.What.Number));
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkItemRosterUpdated>(Handle);
        }
    }
}
