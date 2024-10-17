using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.SiegeEnginesContainers.Messages;
using Serilog;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace GameInterface.Services.SiegeEnginesContainers.Handlers;

internal class SiegeEnginesContainerLifetimeHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<SiegeEnginesContainerLifetimeHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    public SiegeEnginesContainerLifetimeHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        messageBroker.Subscribe<SiegeEnginesContainerCreated>(Handle);
        messageBroker.Subscribe<NetworkCreateSiegeEnginesContainer>(Handle);
    }

    private void Handle(MessagePayload<SiegeEnginesContainerCreated> payload)
    {
        var siegeEnginesInstance = payload.What.SiegeEnginesContainerInstance;

        objectManager.AddNewObject(siegeEnginesInstance, out var id);

        network.SendAll(new NetworkCreateSiegeEnginesContainer(id));
    }

    private void Handle(MessagePayload<NetworkCreateSiegeEnginesContainer> payload)
    {
        if (!ContainerProvider.TryResolve<IObjectManager>(out var objectManager))
        {
            Logger.Error("Unable to resolve {type}", typeof(IObjectManager).FullName);
            return;
        }

        var newSiegeEnginesContainer = ObjectHelper.SkipConstructor<SiegeEnginesContainer>();
        objectManager.AddExisting(payload.What.SiegeEnginesContainerId, newSiegeEnginesContainer);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<SiegeEnginesContainerCreated>(Handle);
        messageBroker.Unsubscribe<NetworkCreateSiegeEnginesContainer>(Handle);
    }
}