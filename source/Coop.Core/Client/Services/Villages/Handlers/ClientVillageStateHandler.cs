using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Villages.Messages;
using GameInterface.Services.Villages.Messages;

namespace Coop.Core.Client.Services.Villages.Handlers
{
    /// <summary>
    /// Handles Network Communications from the Server regarding village states.
    /// </summary>
    internal class ClientVillageStateHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ClientVillageStateHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<NetworkChangeVillageState>(Handle);
        }

        private void Handle(MessagePayload<NetworkChangeVillageState> payload)
        {
            var obj = payload.What;

            var message = new ChangeVillageState(obj.SettlementId, obj.State);

            messageBroker.Publish(this, message);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkChangeVillageState>(Handle);
        }
    }
}
