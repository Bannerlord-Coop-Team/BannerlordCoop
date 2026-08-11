using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Arenas.Messages;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Arenas.Handlers;

internal class ArenaMasterHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<ArenaMasterHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ISessionInteractionsPlayerDataInterface sessionInteractionsPlayerDataInterface;

    public ArenaMasterHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ISessionInteractionsPlayerDataInterface sessionInteractionsPlayerDataInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.sessionInteractionsPlayerDataInterface = sessionInteractionsPlayerDataInterface;

        messageBroker.Subscribe<AddMetArenaMasterAndKnowTournaments>(Handle_AddMetArenaMasterAndKnowTournaments);
        messageBroker.Subscribe<NetworkAddMetArenaMasterAndKnowTournaments>(Handle_NetworkAddMetArenaMasterAndKnowTournaments);

        messageBroker.Subscribe<AddMetArenaMaster>(Handle_AddMetArenaMaster);
        messageBroker.Subscribe<NetworkAddMetArenaMaster>(Handle_NetworkAddMetArenaMaster);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<AddMetArenaMasterAndKnowTournaments>(Handle_AddMetArenaMasterAndKnowTournaments);
        messageBroker.Unsubscribe<NetworkAddMetArenaMasterAndKnowTournaments>(Handle_NetworkAddMetArenaMasterAndKnowTournaments);

        messageBroker.Unsubscribe<AddMetArenaMaster>(Handle_AddMetArenaMaster);
        messageBroker.Unsubscribe<NetworkAddMetArenaMaster>(Handle_NetworkAddMetArenaMaster);
    }

    private void Handle_AddMetArenaMasterAndKnowTournaments(MessagePayload<AddMetArenaMasterAndKnowTournaments> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(data.CurrentSettlement, out var currentSettlementId)) return;

        var message = new NetworkAddMetArenaMasterAndKnowTournaments(
            mainHeroId,
            currentSettlementId
        );
        network.SendAll(message);
    }

    private void Handle_NetworkAddMetArenaMasterAndKnowTournaments(MessagePayload<NetworkAddMetArenaMasterAndKnowTournaments> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            // Validate hero and settlement ids before adding to CoopSession
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.MainHeroId, out var _)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.CurrentSettlementId, out var _)) return;

            sessionInteractionsPlayerDataInterface.AddMetArenaMaster(data.MainHeroId, data.CurrentSettlementId);
            sessionInteractionsPlayerDataInterface.SetKnowTournaments(data.MainHeroId, true);
        });
    }

    private void Handle_AddMetArenaMaster(MessagePayload<AddMetArenaMaster> obj)
    {
        var data = obj.What;

        if (!objectManager.TryGetIdWithLogging(data.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(data.CurrentSettlement, out var currentSettlementId)) return;

        var message = new NetworkAddMetArenaMaster(
            mainHeroId,
            currentSettlementId
        );
        network.SendAll(message);
    }

    private void Handle_NetworkAddMetArenaMaster(MessagePayload<NetworkAddMetArenaMaster> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            // Validate hero and settlement ids before adding to CoopSession
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.MainHeroId, out var _)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.CurrentSettlementId, out var _)) return;

            sessionInteractionsPlayerDataInterface.AddMetArenaMaster(data.MainHeroId, data.CurrentSettlementId);
        });
    }
}
