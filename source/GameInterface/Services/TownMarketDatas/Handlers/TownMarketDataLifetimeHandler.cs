using Common.Logging;
using Common.Messaging;
using Common.Network;
using Common.Util;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.TownMarketDatas.Messages;
using Serilog;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.TownMarketDatas.Handlers;

internal class TownMarketDataLifetimeHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<TownMarketDataLifetimeHandler>();
    private readonly IMessageBroker messageBroker;
    private readonly INetwork network;
    private readonly IObjectManager objectManager;
    public TownMarketDataLifetimeHandler(IMessageBroker messageBroker, INetwork network, IObjectManager objectManager)
    {
        this.messageBroker = messageBroker;
        this.network = network;
        this.objectManager = objectManager;
        messageBroker.Subscribe<TownMarketDataCreated>(HandleCreatedEvent);
        messageBroker.Subscribe<NetworkCreateTownMarketData>(HandleCreateCommand);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<TownMarketDataCreated>(HandleCreatedEvent);
        messageBroker.Unsubscribe<NetworkCreateTownMarketData>(HandleCreateCommand);
    }

    private void HandleCreatedEvent(MessagePayload<TownMarketDataCreated> payload)
    {
        if (!objectManager.AddNewObject(payload.What.Data, out var id))
        {
            Logger.Error("Failed to AddNewObject on {EventHandler}", nameof(TownMarketDataCreated));
            return;
        }

        network.SendAll(new NetworkCreateTownMarketData(id));
    }

    private void HandleCreateCommand(MessagePayload<NetworkCreateTownMarketData> payload)
    {
        var newTownMarketData = ObjectHelper.SkipConstructor<TownMarketData>();

        if (!objectManager.AddExisting(payload.What.MarketDataId, newTownMarketData))
        {
            Logger.Error("Failed to create {ObjectName} on {EventHandler}", nameof(TownMarketData), nameof(NetworkCreateTownMarketData));
            return;
        }
    }
}