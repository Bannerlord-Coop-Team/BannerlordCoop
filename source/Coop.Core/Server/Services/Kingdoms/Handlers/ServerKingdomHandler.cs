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
            ArmyInKingdomCreated armyInKingdomCreated = obj.What;

            // Broadcast to all the clients that the state was changed
            NetworkChangeCreateArmyInKingdom networkChangeCreateArmyInKingdom = 
                new NetworkChangeCreateArmyInKingdom(armyInKingdomCreated.KingdomId, armyInKingdomCreated.ArmyLeaderId, armyInKingdomCreated.TargetSettlement, armyInKingdomCreated.SelectedArmyType);
            
            network.SendAll(networkChangeCreateArmyInKingdom);
        }

       
        public void Dispose()
        {
           
            messageBroker.Unsubscribe<ArmyInKingdomCreated>(HandleServerCreateArmyInKingdom);
        }
    }
}