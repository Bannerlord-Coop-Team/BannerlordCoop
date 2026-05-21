using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.FlattenedTroopRosters.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem.Roster;

namespace GameInterface.Services.FlattenedTroopRosters.Handlers
{
    internal class FlattenedTroopRosterLifetimeHandler : IHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<FlattenedTroopRosterLifetimeHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;

        public FlattenedTroopRosterLifetimeHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<FlattenedTroopRosterCreated>(Handle_FlattenedTroopRosterCreated);
            messageBroker.Subscribe<NetworkCreateFlattenedTroopRoster>(Handle_NetworkCreateFlattenedTroopRoster);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<FlattenedTroopRosterCreated>(Handle_FlattenedTroopRosterCreated);
            messageBroker.Unsubscribe<NetworkCreateFlattenedTroopRoster>(Handle_NetworkCreateFlattenedTroopRoster);
        }

        private void Handle_FlattenedTroopRosterCreated(MessagePayload<FlattenedTroopRosterCreated> obj)
        {
            var payload = obj.What;

            if (objectManager.AddNewObject(payload.FlattenedTroopRoster, out string rosterId) == false) return;

            var message = new NetworkCreateFlattenedTroopRoster(rosterId, payload.Count);
            network.SendAll(message);
        }

        private void Handle_NetworkCreateFlattenedTroopRoster(MessagePayload<NetworkCreateFlattenedTroopRoster> obj)
        {
            var payload = obj.What;

            var troopRoster = new FlattenedTroopRoster(payload.Count);
            if (objectManager.AddExisting(payload.FlattenedTroopRosterId, troopRoster) == false)
            {
                Logger.Error("Failed to add existing TroopRoster, {id}", payload.FlattenedTroopRosterId);
                return;
            }
        }
    }
}
