using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Towns.Messages;
using SandBox.CampaignBehaviors;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns.Handlers;

internal class TavernEmployeesHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<TavernEmployeesHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ISessionInteractionsPlayerDataInterface sessionInteractionsPlayerDataInterface;

    public TavernEmployeesHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ISessionInteractionsPlayerDataInterface sessionInteractionsPlayerDataInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.sessionInteractionsPlayerDataInterface = sessionInteractionsPlayerDataInterface;

        messageBroker.Subscribe<DailyTickDrinkThisDayInSettlement>(Handle_DailyTickDrinkThisDayInSettlement);
        messageBroker.Subscribe<NetworkDailyTickDrinkThisDayInSettlement>(Handle_NetworkDailyTickDrinkThisDayInSettlement);

        messageBroker.Subscribe<WeeklyTickHasBoughtTunToParty>(Handle_WeeklyTickHasBoughtTunToParty);
        messageBroker.Subscribe<NetworkWeeklyTickHasBoughtTunToParty>(Handle_NetworkWeeklyTickHasBoughtTunToParty);

        messageBroker.Subscribe<PlayerAcceptsClanInfoOffer>(Handle_PlayerAcceptsClanInfoOffer);
        messageBroker.Subscribe<NetworkPlayerAcceptsClanInfoOffer>(Handle_NetworkPlayerAcceptsClanInfoOffer);

        messageBroker.Subscribe<TavernMaidDeliversFood>(Handle_TavernMaidDeliversFood);
        messageBroker.Subscribe<NetworkTavernMaidDeliversFood>(Handle_NetworkTavernMaidDeliversFood);

        messageBroker.Subscribe<PlayerBuysTun>(Handle_PlayerBuysTun);
        messageBroker.Subscribe<NetworkPlayerBuysTun>(Handle_NetworkPlayerBuysTun);

        messageBroker.Subscribe<UpdateHasMetRansomBroker>(Handle_UpdateHasMetRansomBroker);
        messageBroker.Subscribe<NetworkUpdateHasMetRansomBroker>(Handle_NetworkUpdateHasMetRansomBroker);

        messageBroker.Subscribe<TavernKeeperFindCompanion>(Handle_TavernKeeperFindCompanion);
        messageBroker.Subscribe<NetworkTavernKeeperFindCompanion>(Handle_NetworkTavernKeeperFindCompanion);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<DailyTickDrinkThisDayInSettlement>(Handle_DailyTickDrinkThisDayInSettlement);
        messageBroker.Unsubscribe<NetworkDailyTickDrinkThisDayInSettlement>(Handle_NetworkDailyTickDrinkThisDayInSettlement);

        messageBroker.Unsubscribe<WeeklyTickHasBoughtTunToParty>(Handle_WeeklyTickHasBoughtTunToParty);
        messageBroker.Unsubscribe<NetworkWeeklyTickHasBoughtTunToParty>(Handle_NetworkWeeklyTickHasBoughtTunToParty);

        messageBroker.Unsubscribe<PlayerAcceptsClanInfoOffer>(Handle_PlayerAcceptsClanInfoOffer);
        messageBroker.Unsubscribe<NetworkPlayerAcceptsClanInfoOffer>(Handle_NetworkPlayerAcceptsClanInfoOffer);

        messageBroker.Unsubscribe<TavernMaidDeliversFood>(Handle_TavernMaidDeliversFood);
        messageBroker.Unsubscribe<NetworkTavernMaidDeliversFood>(Handle_NetworkTavernMaidDeliversFood);

        messageBroker.Unsubscribe<PlayerBuysTun>(Handle_PlayerBuysTun);
        messageBroker.Unsubscribe<NetworkPlayerBuysTun>(Handle_NetworkPlayerBuysTun);

        messageBroker.Unsubscribe<UpdateHasMetRansomBroker>(Handle_UpdateHasMetRansomBroker);
        messageBroker.Unsubscribe<NetworkUpdateHasMetRansomBroker>(Handle_NetworkUpdateHasMetRansomBroker);

        messageBroker.Unsubscribe<TavernKeeperFindCompanion>(Handle_TavernKeeperFindCompanion);
        messageBroker.Unsubscribe<NetworkTavernKeeperFindCompanion>(Handle_NetworkTavernKeeperFindCompanion);
    }

    private void Handle_DailyTickDrinkThisDayInSettlement(MessagePayload<DailyTickDrinkThisDayInSettlement> obj)
    {
        // This daily tick only nullifies any existing values. If all were already null, don't publish anything
        var shouldUpdate = sessionInteractionsPlayerDataInterface.DailyTickDrinkThisDayInSettlement();
        if (!shouldUpdate) return;

        var message = new NetworkDailyTickDrinkThisDayInSettlement();
        network.SendAll(message);
    }

    private void Handle_NetworkDailyTickDrinkThisDayInSettlement(MessagePayload<NetworkDailyTickDrinkThisDayInSettlement> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!TryGetTavernEmployeesBehavior(out var tavernEmployeesBehavior)) return;

            tavernEmployeesBehavior._orderedDrinkThisDayInSettlement = null;
        });
    }

    private void Handle_WeeklyTickHasBoughtTunToParty(MessagePayload<WeeklyTickHasBoughtTunToParty> obj)
    {
        // This weekly tick only falsifies any existing values. If all were already false, don't publish anything
        var shouldUpdate = sessionInteractionsPlayerDataInterface.WeeklyTickHasBoughtToTunToParty();
        if (!shouldUpdate) return;

        var message = new NetworkWeeklyTickHasBoughtTunToParty();
        network.SendAll(message);
    }

    private void Handle_NetworkWeeklyTickHasBoughtTunToParty(MessagePayload<NetworkWeeklyTickHasBoughtTunToParty> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!TryGetTavernEmployeesBehavior(out var tavernEmployeesBehavior)) return;

            tavernEmployeesBehavior._hasBoughtTunToParty = false;
        });
    }

    private void Handle_PlayerAcceptsClanInfoOffer(MessagePayload<PlayerAcceptsClanInfoOffer> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;

        var message = new NetworkPlayerAcceptsClanInfoOffer(mainHeroId);
        network.SendAll(message);
    }

    private void Handle_NetworkPlayerAcceptsClanInfoOffer(MessagePayload<NetworkPlayerAcceptsClanInfoOffer> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.MainHeroId, out var mainHero)) return;

            GiveGoldAction.ApplyBetweenCharacters(mainHero, null, 500, false);
        });
    }

    private void Handle_TavernMaidDeliversFood(MessagePayload<TavernMaidDeliversFood> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;
        if (!objectManager.TryGetIdWithLogging(obj.What.CurrentSettlement, out var currentSettlementId)) return;

        var message = new NetworkTavernMaidDeliversFood(mainHeroId, currentSettlementId);
        network.SendAll(message);
    }

    private void Handle_NetworkTavernMaidDeliversFood(MessagePayload<NetworkTavernMaidDeliversFood> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            // Validate ids before adding to CoopSession
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.MainHeroId, out var _)) return;
            if (!objectManager.TryGetObjectWithLogging<Settlement>(data.CurrentSettlementId, out var _)) return;

            sessionInteractionsPlayerDataInterface.UpdateDrinkThisDayInSettlement(data.MainHeroId, data.CurrentSettlementId);
        });
    }

    private void Handle_PlayerBuysTun(MessagePayload<PlayerBuysTun> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;

        var message = new NetworkPlayerBuysTun(mainHeroId, obj.What.TunPrice);
        network.SendAll(message);
    }

    private void Handle_NetworkPlayerBuysTun(MessagePayload<NetworkPlayerBuysTun> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.MainHeroId, out var mainHero)) return;

            GiveGoldAction.ApplyBetweenCharacters(mainHero, null, data.TunPrice, false);
            if (mainHero.PartyBelongedTo != null) mainHero.PartyBelongedTo.RecentEventsMorale += 2f;
            sessionInteractionsPlayerDataInterface.UpdateHasBoughtTunToParty(data.MainHeroId, true);
        });
    }

    private void Handle_UpdateHasMetRansomBroker(MessagePayload<UpdateHasMetRansomBroker> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;

        var message = new NetworkUpdateHasMetRansomBroker(mainHeroId, obj.What.HasMetRansomBroker);
        network.SendAll(message);
    }

    private void Handle_NetworkUpdateHasMetRansomBroker(MessagePayload<NetworkUpdateHasMetRansomBroker> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            // Validate id before adding to CoopSession
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.MainHeroId, out var _)) return;

            sessionInteractionsPlayerDataInterface.UpdateHasMetRandomBroker(data.MainHeroId, data.HasMetRansomBroker);
        });
    }

    private void Handle_TavernKeeperFindCompanion(MessagePayload<TavernKeeperFindCompanion> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MainHero, out var mainHeroId)) return;

        var message = new NetworkTavernKeeperFindCompanion(mainHeroId);
        network.SendAll(message);
    }

    private void Handle_NetworkTavernKeeperFindCompanion(MessagePayload<NetworkTavernKeeperFindCompanion> obj)
    {
        var data = obj.What;

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Hero>(data.MainHeroId, out var mainHero)) return;

            GiveGoldAction.ApplyBetweenCharacters(mainHero, null, 2, false);
        });
    }

    private bool TryGetTavernEmployeesBehavior(out TavernEmployeesCampaignBehavior tavernEmployeesCampaignBehavior)
    {
        tavernEmployeesCampaignBehavior = Campaign.Current?.GetCampaignBehavior<TavernEmployeesCampaignBehavior>();
        if (tavernEmployeesCampaignBehavior != null) return true;

        Logger.Debug("Skipping tavern employees update because the campaign behavior is unavailable");
        return false;
    }
}
