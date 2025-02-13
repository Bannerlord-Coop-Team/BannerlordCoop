using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.SiegeStrategies.Messages;
using Serilog;
using TaleWorlds.CampaignSystem.Siege;

namespace GameInterface.Services.SiegeStrategies.Handlers;
internal class SiegeStrategyLifetimeHandler : IHandler
{
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly IMessageBroker messageBroker;
    private readonly ILogger logger;
    public SiegeStrategyLifetimeHandler(IObjectManager objectManager, INetwork network, IMessageBroker messageBroker, ILogger logger)
    {
        this.objectManager = objectManager;
        this.network = network;
        this.messageBroker = messageBroker;
        this.logger = logger;

        messageBroker.Subscribe<SiegeStrategyCreated>(Handle_SiegeStrategyCreated);
        messageBroker.Subscribe<NetworkCreateSiegeStrategy>(Handle_NetworkCreateSiegeStrategy);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SiegeStrategyCreated>(Handle_SiegeStrategyCreated);
        messageBroker.Unsubscribe<NetworkCreateSiegeStrategy>(Handle_NetworkCreateSiegeStrategy);
    }

    private void Handle_SiegeStrategyCreated(MessagePayload<SiegeStrategyCreated> payload)
    {
        var instance = payload.What.Instance;

        if (objectManager.AddNewObject(instance, out var newId) == false)
        {
            logger.Error("Unable to add new {type} to object manager", instance.GetType());
            return;
        }

        var message = new NetworkCreateSiegeStrategy(newId);
        network.SendAll(message);
    }

    private void Handle_NetworkCreateSiegeStrategy(MessagePayload<NetworkCreateSiegeStrategy> payload)
    {
        var id = payload.What.Id;
        var newPartyBase = ObjectHelper.SkipConstructor<SiegeStrategy>();

        if (objectManager.AddExisting(id, newPartyBase) == false)
        {
            logger.Error("Unable to create new {type} with id {id}", newPartyBase.GetType(), id);
            return;
        }
    }
}
