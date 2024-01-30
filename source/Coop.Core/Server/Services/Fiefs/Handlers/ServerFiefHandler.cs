using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Fiefs.Messages;
using GameInterface.Services.Fiefs.Messages;
using System;
using System.Linq;

namespace Coop.Core.Server.Services.Fiefs.Handlers
{
    /// <summary>
    /// Handles network related data for Fief
    /// </summary>
    public class ServerFiefHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ServerFiefHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            // This handles an internal message
            messageBroker.Subscribe<FiefFoodStockChanged>(HandleFiefFoodStock);
        }

        private void HandleFiefFoodStock(MessagePayload<FiefFoodStockChanged> obj)
        {
            FiefFoodStockChanged fiefFoodStockChanged = obj.What;

            // Broadcast to all the clients that the state was changed
            NetworkChangeFiefFoodStock networkChangeFiefFoodStock = new NetworkChangeFiefFoodStock(fiefFoodStockChanged.FiefId, fiefFoodStockChanged.FoodStockQuantity);
            network.SendAll(networkChangeFiefFoodStock);
        }


        

        public void Dispose()
        {
            messageBroker.Unsubscribe<FiefFoodStockChanged>(HandleFiefFoodStock);

        }
    }
}
