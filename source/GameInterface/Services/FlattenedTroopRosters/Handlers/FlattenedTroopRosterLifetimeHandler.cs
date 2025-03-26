using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.FlattenedTroopRosters.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TroopRosters.Handlers;
using GameInterface.Services.TroopRosters.Messages;
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
            messageBroker.Subscribe<FlattenedTroopRosterCreated>(Handle);
            messageBroker.Subscribe<NetworkCreateFlattenedTroopRoster>(Handle);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<FlattenedTroopRosterCreated>(Handle);
            messageBroker.Unsubscribe<NetworkCreateFlattenedTroopRoster>(Handle);
        }

        private void Handle(MessagePayload<FlattenedTroopRosterCreated> obj)
        {
            var payload = obj.What;

            if (objectManager.AddNewObject(payload.FlattenedTroopRoster, out string rosterId) == false) return;

            var message = new NetworkCreateFlattenedTroopRoster(rosterId, payload.Count);
            network.SendAll(message);
        }

        private void Handle(MessagePayload<NetworkCreateFlattenedTroopRoster> obj)
        {
            var payload = obj.What;

            var troopRoster = ObjectHelper.SkipConstructor<FlattenedTroopRoster>();
            if (objectManager.AddExisting(payload.FlattenedTroopRosterId, troopRoster) == false)
            {
                Logger.Error("Failed to add existing TroopRoster, {id}", payload.FlattenedTroopRosterId);
                return;
            }
        }
    }
}
