using Common.Messaging;
using Common.Network;
using Coop.Core.Server.Services.Armies.Messages;
using GameInterface.Services.Armies.Messages;


namespace Coop.Core.Client.Services.Army.Handlers
{
    /// <summary>
    /// Handles Network Communications from the Server regarding Armies.
    /// </summary>
    public class ClientArmyHandler : IHandler
    {
        private readonly IMessageBroker messageBroker;
        private readonly INetwork network;

        public ClientArmyHandler(IMessageBroker messageBroker, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.network = network;

            messageBroker.Subscribe<NetworkChangeAddMobilePartyInArmy>(HandleMobilePartyInArmyAdd);
            messageBroker.Subscribe<NetworkChangeRemoveMobilePartyInArmy>(HandleMobilePartyInArmyRemove);
        }


        private void HandleMobilePartyInArmyAdd(MessagePayload<NetworkChangeAddMobilePartyInArmy> payload)
        {
            NetworkChangeAddMobilePartyInArmy networkChangeAddMobilePartyInArmy = payload.What;
            AddMobilePartyInArmy message =
                new AddMobilePartyInArmy(networkChangeAddMobilePartyInArmy.MobilePartyId, networkChangeAddMobilePartyInArmy.LeaderMobilePartyId);

            messageBroker.Publish(this, message);

        }

        private void HandleMobilePartyInArmyRemove(MessagePayload<NetworkChangeRemoveMobilePartyInArmy> payload)
        {
            NetworkChangeRemoveMobilePartyInArmy networkRemoveMobilePartyInArmy = payload.What;
            RemoveMobilePartyInArmy message =
                new RemoveMobilePartyInArmy(networkRemoveMobilePartyInArmy.MobilePartyId, networkRemoveMobilePartyInArmy.LeaderMobilePartyId);

            messageBroker.Publish(this, message);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<NetworkChangeAddMobilePartyInArmy>(HandleMobilePartyInArmyAdd);
            messageBroker.Unsubscribe<NetworkChangeRemoveMobilePartyInArmy>(HandleMobilePartyInArmyRemove);
        }
    }
}
