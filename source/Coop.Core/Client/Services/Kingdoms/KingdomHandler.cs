using Common.Messaging;
using Common.Network;
using GameInterface.Services.Kingdoms.Messages;

namespace Coop.Core.Client.Services.Kingdoms
{
    public class KingdomHandler : IHandler
    {

        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public KingdomHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<NetworkChangeCreateArmyInKingdom>(HandleChangeCreateArmyInKingdom);
        }
        private void HandleChangeCreateArmyInKingdom(MessagePayload<NetworkChangeCreateArmyInKingdom> payload)
        {
            NetworkChangeCreateArmyInKingdom networkChangeCreateArmyInKingdom = payload.What;
            CreateArmyInKingdom message =
                new CreateArmyInKingdom(networkChangeCreateArmyInKingdom.KingdomId, networkChangeCreateArmyInKingdom.ArmyLeaderId,
                        networkChangeCreateArmyInKingdom.TargetSettlement, networkChangeCreateArmyInKingdom.SelectedArmyType);
            messageBroker.Publish(this, message);
        }
        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkChangeCreateArmyInKingdom>(HandleChangeCreateArmyInKingdom);
        }
    }

    
}
