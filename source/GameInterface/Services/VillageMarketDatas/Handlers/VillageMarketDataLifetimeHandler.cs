using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.SiegeEvents.Messages;
using GameInterface.Services.VillageMarketDatas.Messages;
using Serilog;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.VillageMarketDatas.Handlers;
/// <summary>
/// Lifetime handler for <see cref="VillageMarketData"/> objects.
/// </summary>
internal class VillageMarketDataLifetimeHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<VillageMarketDataLifetimeHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    public VillageMarketDataLifetimeHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        messageBroker.Subscribe<VillageMarketDataCreated>(HandleCreatedEvent);
        messageBroker.Subscribe<NetworkCreateVillageMarketData>(HandleCreateCommand);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<VillageMarketDataCreated>(HandleCreatedEvent);
        messageBroker.Unsubscribe<NetworkCreateVillageMarketData>(HandleCreateCommand);
    }

    private void HandleCreatedEvent(MessagePayload<VillageMarketDataCreated> payload)
    {
        if (!objectManager.AddNewObject(payload.What.Data, out var id))
        {
            Logger.Error("Failed to AddNewObject on {EventHandler}", nameof(VillageMarketDataCreated));
            return;
        }

        network.SendAll(new NetworkCreateVillageMarketData(id));
    }

    private void HandleCreateCommand(MessagePayload<NetworkCreateVillageMarketData> payload)
    {
        var newVillageMarketData = ObjectHelper.SkipConstructor<VillageMarketData>();

        if (!objectManager.AddExisting(payload.What.MarketDataId, newVillageMarketData))
        {
            Logger.Error("Failed to create {ObjectName} on {EventHandler}", nameof(VillageMarketData), nameof(NetworkCreateVillageMarketData));
            return;
        }
    }
}