using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Armies.Messages;
using GameInterface.Services.Armies.Messages;


namespace Coop.Core.Server.Services.Armies.Handlers
{
    /// <summary>
    /// Handles network related data for Armies
    /// </summary>
    public class ServerArmyHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ServerArmyHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            // This handles an internal message
            messageBroker.Subscribe<MobilePartyInArmyAdded>(HandleAddMobilePartyInArmy);
        }

        private void HandleAddMobilePartyInArmy(MessagePayload<MobilePartyInArmyAdded> obj)
        {
            MobilePartyInArmyAdded mobilePartyInArmyAdded = obj.What;

            // Broadcast to all the clients that the state was changed
            NetworkChangeAddMobilePartyInArmy networkChangeAddMobilePartyInArmy = 
                new NetworkChangeAddMobilePartyInArmy(mobilePartyInArmyAdded.MobilePartyId, mobilePartyInArmyAdded.LeaderMobilePartyId);
            
            network.SendAll(networkChangeAddMobilePartyInArmy);
        }

       
        public void Dispose()
        {
           
            messageBroker.Unsubscribe<MobilePartyInArmyAdded>(HandleAddMobilePartyInArmy);
        }
    }
}