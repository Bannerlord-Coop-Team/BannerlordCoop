using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Settlements.Messages;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Settlements.Handlers;

internal class SettlementSneakInHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<SettlementSneakInHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ISessionInteractionsPlayerDataInterface sessionInteractionsPlayerDataInterface;

    public SettlementSneakInHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ISessionInteractionsPlayerDataInterface sessionInteractionsPlayerDataInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.sessionInteractionsPlayerDataInterface = sessionInteractionsPlayerDataInterface;

        messageBroker.Subscribe<AddSettlementAsSneakedIn>(Handle_AddSettlementAsSneakedIn);
        messageBroker.Subscribe<NetworkAddSettlementAsSneakedIn>(Handle_NetworkAddSettlementAsSneakedIn);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AddSettlementAsSneakedIn>(Handle_AddSettlementAsSneakedIn);
        messageBroker.Unsubscribe<NetworkAddSettlementAsSneakedIn>(Handle_NetworkAddSettlementAsSneakedIn);
    }

    private void Handle_AddSettlementAsSneakedIn(MessagePayload<AddSettlementAsSneakedIn> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(data.CurrentSettlement, out var currentSettlementId)) return;

        var message = new NetworkAddSettlementAsSneakedIn(mainHeroId, currentSettlementId);
        network.SendAll(message);
    }

    private void Handle_NetworkAddSettlementAsSneakedIn(MessagePayload<NetworkAddSettlementAsSneakedIn> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            // Validate hero and settlement ids before adding to CoopSession
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.MainHeroId, out var _)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.CurrentSettlementId, out var _)) return;

            sessionInteractionsPlayerDataInterface.AddSettlementSneakedIn(data.MainHeroId, data.CurrentSettlementId);
        });
    }
}
