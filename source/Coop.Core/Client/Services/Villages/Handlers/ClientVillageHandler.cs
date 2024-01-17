using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Villages.Messages;
using GameInterface.Services.Villages.Messages;

namespace Coop.Core.Client.Services.Villages.Handlers
{
    /// <summary>
    /// Handles Network Communications from the Server regarding village states.
    /// </summary>
    internal class ClientVillageHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ClientVillageHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<NetworkChangeVillageState>(HandleVillageState);

            messageBroker.Subscribe<NetworkChangeVillageTradeBound>(HandleTrade);
        }

        private void HandleVillageState(MessagePayload<NetworkChangeVillageState> payload)
        {
            var obj = payload.What;

            var message = new ChangeVillageState(obj.SettlementId, obj.State);

            messageBroker.Publish(this, message);
        }


        private void HandleTrade(MessagePayload<NetworkChangeVillageTradeBound> payload)
        {
            var obj = payload.What;

            var message = new ChangeVillageTradeBound(obj.VillageId, obj.TradeBoundId);

            messageBroker.Publish(this, message);
        }


        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkChangeVillageState>(HandleVillageState);
            messageBroker.Unsubscribe<NetworkChangeVillageTradeBound>(HandleTrade);
        }
    }
}
