using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Fiefs.Messages;
using GameInterface.Services.Fiefs.Messages;

namespace Coop.Core.Client.Services.Fiefs.Handlers
{
    /// <summary>
    /// Handles Network Communications from the Server regarding fief states.
    /// </summary>
    public class ClientFiefHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ClientFiefHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<NetworkChangeFiefFoodStock>(HandleFoodStock);
        }


        private void HandleFoodStock(MessagePayload<NetworkChangeFiefFoodStock> payload)
        {
            NetworkChangeFiefFoodStock networkChangeFiefFoodStock = payload.What;
            ChangeFiefFoodStock message = new ChangeFiefFoodStock(networkChangeFiefFoodStock.FiefId, networkChangeFiefFoodStock.FoodStockQuantity);
            messageBroker.Publish(this, message);

        }

        public void Dispose()
        {
            messageBroker.Subscribe<NetworkChangeFiefFoodStock>(HandleFoodStock);
        }
    }
}
