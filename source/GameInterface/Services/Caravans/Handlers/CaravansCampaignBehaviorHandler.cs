using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Caravans.Data;
using GameInterface.Services.Caravans.Interfaces;
using GameInterface.Services.Caravans.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Caravans.Handlers;

internal class CaravansCampaignBehaviorHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<CaravansCampaignBehaviorHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ISessionCaravansPlayerDataInterface sessionCaravansPlayerDataInterface;

    public CaravansCampaignBehaviorHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ISessionCaravansPlayerDataInterface sessionCaravansPlayerDataInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.sessionCaravansPlayerDataInterface = sessionCaravansPlayerDataInterface;
        messageBroker.Subscribe<CaravansKingdomDestroyed>(Handle_CaravansKingdomDestroyed);
        messageBroker.Subscribe<NetworkCaravansKingdomDestroyed>(Handle_NetworkCaravansKingdomDestroyed);
        messageBroker.Subscribe<CaravanPartyDestroyed>(Handle_CaravanPartyDestroyed);
        messageBroker.Subscribe<NetworkCaravanPartyDestroyed>(Handle_NetworkCaravanPartyDestroyed);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CaravansKingdomDestroyed>(Handle_CaravansKingdomDestroyed);
        messageBroker.Unsubscribe<NetworkCaravansKingdomDestroyed>(Handle_NetworkCaravansKingdomDestroyed);
        messageBroker.Unsubscribe<CaravanPartyDestroyed>(Handle_CaravanPartyDestroyed);
        messageBroker.Unsubscribe<NetworkCaravanPartyDestroyed>(Handle_NetworkCaravanPartyDestroyed);
    }

    private void Handle_CaravansKingdomDestroyed(MessagePayload<CaravansKingdomDestroyed> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.DestroyedKingdom, out var destroyedKingdomId)) return;

        // Update CoopSession data on server
        sessionCaravansPlayerDataInterface.RemoveProhibitedKingdomForAllPlayers(destroyedKingdomId);

        var message = new NetworkCaravansKingdomDestroyed(destroyedKingdomId);
        network.SendAll(message);
    }

    private void Handle_NetworkCaravansKingdomDestroyed(MessagePayload<NetworkCaravansKingdomDestroyed> obj)
    {
        // Update data on all clients
        GameThread.Run(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<Kingdom>(obj.What.DestroyedKingdomId, out var destroyedKingdom)) return;
            GetCaravansBehavior().OnKingdomDestroyed(destroyedKingdom);
        });
    }

    private void Handle_CaravanPartyDestroyed(MessagePayload<CaravanPartyDestroyed> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;

        // Update CoopSession data on server
        sessionCaravansPlayerDataInterface.RemoveInteractedCaravanForAllPlayers(mobilePartyId);

        var message = new NetworkCaravanPartyDestroyed(mobilePartyId);
        network.SendAll(message);
    }

    private void Handle_NetworkCaravanPartyDestroyed(MessagePayload<NetworkCaravanPartyDestroyed> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MobilePartyId, out var mobileParty)) return;

        // Don't need to run the full OnMobilePartyDestroyed logic on clients, only need to update these two dictionaries
        GameThread.Run(() =>
        {
            var caravansBehavior = GetCaravansBehavior();
            if (caravansBehavior._interactedCaravans.ContainsKey(mobileParty))
            {
                caravansBehavior._interactedCaravans.Remove(mobileParty);
            }
            if (caravansBehavior._tradeActionLogs.ContainsKey(mobileParty))
            {
                caravansBehavior._tradeActionLogs.Remove(mobileParty);
            }
        });
    }

    private bool PackTradeActionLog(CaravansCampaignBehavior.TradeActionLog tradeActionLog, out TradeActionLogData tradeActionLogData)
    {
        tradeActionLogData = new();
        if (!objectManager.TryGetIdWithLogging(tradeActionLog.BoughtSettlement, out var boughtSettlementId)) return false;
        if (!objectManager.TryGetIdWithLogging(tradeActionLog.SoldSettlement, out var soldSettlementId)) return false;

        tradeActionLogData = new TradeActionLogData(
            boughtSettlementId,
            tradeActionLog.BuyPrice,
            tradeActionLog.SellPrice,
            tradeActionLog.ItemRosterElement,
            soldSettlementId,
            tradeActionLog.BoughtTime);

        return true;
    }

    private bool UnpackTradeActionLogData(TradeActionLogData tradeActionLogData, out CaravansCampaignBehavior.TradeActionLog tradeActionLog)
    {
        tradeActionLog = new();
        if (!objectManager.TryGetObjectWithLogging<Settlement>(tradeActionLogData.BoughtSettlementId, out var boughtSettlement)) return false;
        if (!objectManager.TryGetObjectWithLogging<Settlement>(tradeActionLogData.SoldSettlementId, out var soldSettlement)) return false;

        tradeActionLog = new CaravansCampaignBehavior.TradeActionLog()
        {
            BoughtSettlement = boughtSettlement,
            BuyPrice = tradeActionLogData.BuyPrice,
            SellPrice = tradeActionLogData.SellPrice,
            ItemRosterElement = tradeActionLogData.ItemRosterElement,
            SoldSettlement = soldSettlement,
            BoughtTime = tradeActionLogData.BoughtTime
        };

        return true;
    }

    private CaravansCampaignBehavior GetCaravansBehavior()
    {
        return Campaign.Current.GetCampaignBehavior<CaravansCampaignBehavior>();
    }
}
