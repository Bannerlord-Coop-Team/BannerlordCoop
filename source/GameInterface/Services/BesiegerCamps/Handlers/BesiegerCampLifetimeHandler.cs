using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.BesiegerCamps.Messages;
using GameInterface.Services.ObjectManager;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Library;

namespace GameInterface.Services.BesiegerCamps.Handlers;

internal class BesiegerCampLifetimeHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<BesiegerCampLifetimeHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;

    public BesiegerCampLifetimeHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        messageBroker.Subscribe<BesiegerCampCreated>(Handle);
        messageBroker.Subscribe<NetworkCreateBesiegerCamp>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<BesiegerCampCreated>(Handle);
        messageBroker.Unsubscribe<NetworkCreateBesiegerCamp>(Handle);
    }

    private void Handle(MessagePayload<BesiegerCampCreated> payload)
    {
        objectManager.AddNewObject(payload.What.Instance, out var id);

        network.SendAll(new NetworkCreateBesiegerCamp(id));
    }

    private void Handle(MessagePayload<NetworkCreateBesiegerCamp> payload)
    {
        var newBesiegerCamp = ObjectHelper.SkipConstructor<BesiegerCamp>();

        // TODO change setting to constructor patch
        AccessTools.Field(typeof(BesiegerCamp), nameof(BesiegerCamp._besiegerParties)).SetValue(newBesiegerCamp, new MBList<MobileParty>());

        objectManager.AddExisting(payload.What.BesiegerCampId, newBesiegerCamp);
    }
}