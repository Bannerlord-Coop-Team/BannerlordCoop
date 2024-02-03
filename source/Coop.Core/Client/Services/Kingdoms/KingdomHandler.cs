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
            var message = new CreateArmyInKingdom(payload.What.Data);
            messageBroker.Publish(this, message);
        }
        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkChangeCreateArmyInKingdom>(HandleChangeCreateArmyInKingdom);
        }
    }

    
}
