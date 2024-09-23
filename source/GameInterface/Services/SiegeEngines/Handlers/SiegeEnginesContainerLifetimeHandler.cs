using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.SiegeEnginesContainers.Messages;
using GameInterface.Services.ObjectManager;
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

    public void Dispose()
    {
        messageBroker.Unsubscribe<SiegeEnginesContainerCreated>(Handle);
        messageBroker.Unsubscribe<NetworkCreateSiegeEnginesContainer>(Handle);
    }


    private void Handle(MessagePayload<SiegeEnginesContainerCreated> payload)
    {
        objectManager.AddNewObject(payload.What.Instance, out var id);

        network.SendAll(new NetworkCreateSiegeEnginesContainer(id));
    }


    private void Handle(MessagePayload<NetworkCreateSiegeEnginesContainer> payload)
    {
        var newSiegeEnginesContainer = ObjectHelper.SkipConstructor<SiegeEnginesContainer>();

        // TODO change setting to constructor patch
        //AccessTools.Field(typeof(SiegeEnginesContainer), nameof(SiegeEnginesContainer._besiegerParties)).SetValue(newSiegeEnginesContainer, new MBList<MobileParty>());

        objectManager.AddExisting(payload.What.Id, newSiegeEnginesContainer);
    }
}
