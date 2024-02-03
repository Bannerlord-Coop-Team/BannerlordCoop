using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Armies.Messages;
using GameInterface.Services.Armies.Messages;
using GameInterface.Services.Kingdoms.Messages;


namespace Coop.Core.Server.Services.Kingdoms.Handlers
{
    /// <summary>
    /// Handles network related data for Kingdoms
    /// </summary>
    public class ServerKingdomHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ServerKingdomHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            // This handles an internal message
            messageBroker.Subscribe<ArmyInKingdomCreated>(HandleServerCreateArmyInKingdom);

        }

        private void HandleServerCreateArmyInKingdom(MessagePayload<ArmyInKingdomCreated> obj)
        {
            // Broadcast to all the clients that the state was changed
            var message =  new NetworkChangeCreateArmyInKingdom(obj.What.Data);
            
            network.SendAll(message);
        }

       
        public void Dispose()
        {
           
            messageBroker.Unsubscribe<ArmyInKingdomCreated>(HandleServerCreateArmyInKingdom);
        }
    }
}