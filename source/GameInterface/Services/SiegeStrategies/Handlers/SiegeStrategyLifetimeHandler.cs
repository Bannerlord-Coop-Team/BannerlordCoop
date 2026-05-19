using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.SiegeStrategies.Messages.Lifetime;
using GameInterface.Services.SiegeStrategies.Handlers;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem.Siege;


namespace GameInterface.Services.SiegeStrategies.Handlers
{
    internal class SiegeStrategyLifetimeHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger<SiegeStrategyLifetimeHandler>();
        private readonly IMessageBroker messageBroker;
        private readonly IObjectManager objectManager;
        private readonly INetwork network;

        public SiegeStrategyLifetimeHandler(IMessageBroker messageBroker, IObjectManager objectManager, INetwork network)
        {
            this.messageBroker = messageBroker;
            this.objectManager = objectManager;
            this.network = network;
            messageBroker.Subscribe<SiegeStrategyCreated>(Handle_SiegeStrategyCreated);
            messageBroker.Subscribe<NetworkCreateSiegeStrategy>(Handle_CreateSiegeStrategy);
        }

        public void Dispose()
        {
            messageBroker.Unsubscribe<SiegeStrategyCreated>(Handle_SiegeStrategyCreated);
            messageBroker.Unsubscribe<NetworkCreateSiegeStrategy>(Handle_CreateSiegeStrategy);
        }

        private void Handle_SiegeStrategyCreated(MessagePayload<SiegeStrategyCreated> obj)
        {
            var payload = obj.What;

            if (objectManager.AddNewObject(payload.SiegeStrategy, out string siegeStrategyId) == false) return;

            var message = new NetworkCreateSiegeStrategy(siegeStrategyId);
            network.SendAll(message);
        }

        private void Handle_CreateSiegeStrategy(MessagePayload<NetworkCreateSiegeStrategy> obj)
        {
            var payload = obj.What;

            var siegeStrategy = ObjectHelper.SkipConstructor<SiegeStrategy>();
            if (objectManager.AddExisting(payload.SiegeStrategyId, siegeStrategy) == false)
            {
                Logger.Error("Failed to add existing siege strategy, {id}", payload.SiegeStrategyId);
                return;
            }
        }
    }
}