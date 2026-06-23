using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Caravans.Data;
using GameInterface.Services.Caravans.Interfaces;
using GameInterface.Services.Caravans.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Collections.Generic;
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

        messageBroker.Subscribe<DeleteExpiredTradeRumorTakenCaravans>(Handle_DeleteExpiredTradeRumorTakenCaravans);
        messageBroker.Subscribe<NetworkDeleteExpiredTradeRumorTakenCaravans>(Handle_NetworkDeleteExpiredTradeRumorTakenCaravans);

        messageBroker.Subscribe<DeleteExpiredLootedCaravans>(Handle_DeleteExpiredLootedCaravans);
        messageBroker.Subscribe<NetworkDeleteExpiredLootedCaravans>(Handle_NetworkDeleteExpiredLootedCaravans);

        messageBroker.Subscribe<UpdateTradeActionLogsForParty>(Handle_UpdateTradeActionLogsForParty);
        messageBroker.Subscribe<NetworkUpdateTradeActionLogsForParty>(Handle_NetworkUpdateTradeActionLogsForParty);

        messageBroker.Subscribe<NetworkAddToLootedCaravans>(Handle_NetworkAddToLootedCaravans);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<CaravansKingdomDestroyed>(Handle_CaravansKingdomDestroyed);
        messageBroker.Unsubscribe<NetworkCaravansKingdomDestroyed>(Handle_NetworkCaravansKingdomDestroyed);

        messageBroker.Unsubscribe<CaravanPartyDestroyed>(Handle_CaravanPartyDestroyed);
        messageBroker.Unsubscribe<NetworkCaravanPartyDestroyed>(Handle_NetworkCaravanPartyDestroyed);

        messageBroker.Unsubscribe<DeleteExpiredTradeRumorTakenCaravans>(Handle_DeleteExpiredTradeRumorTakenCaravans);
        messageBroker.Unsubscribe<NetworkDeleteExpiredTradeRumorTakenCaravans>(Handle_NetworkDeleteExpiredTradeRumorTakenCaravans);
        
        messageBroker.Unsubscribe<DeleteExpiredLootedCaravans>(Handle_DeleteExpiredLootedCaravans);
        messageBroker.Unsubscribe<NetworkDeleteExpiredLootedCaravans>(Handle_NetworkDeleteExpiredLootedCaravans);

        messageBroker.Unsubscribe<UpdateTradeActionLogsForParty>(Handle_UpdateTradeActionLogsForParty);
        messageBroker.Unsubscribe<NetworkUpdateTradeActionLogsForParty>(Handle_NetworkUpdateTradeActionLogsForParty);

        messageBroker.Unsubscribe<NetworkAddToLootedCaravans>(Handle_NetworkAddToLootedCaravans);
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

    private void Handle_DeleteExpiredTradeRumorTakenCaravans(MessagePayload<DeleteExpiredTradeRumorTakenCaravans> obj)
    {
        // Update CoopSession and send changes to clients to update local instances of _tradeRumorTakenCaravans
        sessionCaravansPlayerDataInterface.DeleteExpiredTradeRumorTakenCaravans(out var playerExpiredCaravansRemovalLists);

        network.SendAll(new NetworkDeleteExpiredTradeRumorTakenCaravans(playerExpiredCaravansRemovalLists));
    }

    private void Handle_NetworkDeleteExpiredTradeRumorTakenCaravans(MessagePayload<NetworkDeleteExpiredTradeRumorTakenCaravans> obj)
    {
        var caravansBehavior = GetCaravansBehavior();

        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetIdWithLogging(Hero.MainHero, out var mainHeroId)) return;

            foreach (var playerList in obj.What.PlayerExpiredCaravansRemovalLists)
            {
                // Only use data associated with this playerHero to update _tradeRumorTakenCaravans
                if (playerList.Key != mainHeroId) continue;

                foreach (var removedCaravanId in playerList.Value)
                {
                    if (!objectManager.TryGetObjectWithLogging<MobileParty>(removedCaravanId, out var removedCaravan)) continue;
                    caravansBehavior._tradeRumorTakenCaravans.Remove(removedCaravan);
                }
                break;
            }
        });
    }

    private void Handle_DeleteExpiredLootedCaravans(MessagePayload<DeleteExpiredLootedCaravans> obj)
    {
        var caravansBehavior = GetCaravansBehavior();
        GameThread.RunSafe(() =>
        {
            // Vanilla implementation, need to send list to clients
            List<MobileParty> list = new List<MobileParty>();
            foreach (KeyValuePair<MobileParty, CampaignTime> keyValuePair in caravansBehavior._lootedCaravans)
            {
                if (CampaignTime.Now - keyValuePair.Value >= CampaignTime.Days(10f))
                {
                    list.Add(keyValuePair.Key);
                }
            }
            foreach (MobileParty key in list)
            {
                caravansBehavior._lootedCaravans.Remove(key);
            }

            // Update changes to _lootedCaravans on clients
            List<string> deletedLootedCaravansIdsList = new();
            foreach (var deletedCaravan in list)
            {
                if (!objectManager.TryGetIdWithLogging(deletedCaravan, out var caravanPartyId)) continue;

                deletedLootedCaravansIdsList.Add(caravanPartyId);
            }
            network.SendAll(new NetworkDeleteExpiredLootedCaravans(deletedLootedCaravansIdsList));
        });
    }

    private void Handle_NetworkDeleteExpiredLootedCaravans(MessagePayload<NetworkDeleteExpiredLootedCaravans> obj)
    {
        GameThread.RunSafe(() =>
        {
            foreach (var deletedCaravanId in obj.What.DeletedLootedCaravansIdsList)
            {
                if (!objectManager.TryGetObjectWithLogging<MobileParty>(deletedCaravanId, out var deletedCaravan)) continue;

                GetCaravansBehavior()._lootedCaravans.Remove(deletedCaravan);
            }
        });
    }

    private void Handle_UpdateTradeActionLogsForParty(MessagePayload<UpdateTradeActionLogsForParty> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.MobileParty, out var mobilePartyId)) return;

        var tradeActionLogsData = new List<TradeActionLogData>();
        foreach (var tradeActionLog in obj.What.TradeActionLogs)
        {
            if (!PackTradeActionLog(tradeActionLog, out var tradeActionLogData)) continue;

            tradeActionLogsData.Add(tradeActionLogData);
        }

        network.SendAll(new NetworkUpdateTradeActionLogsForParty(mobilePartyId, tradeActionLogsData));
    }

    private void Handle_NetworkUpdateTradeActionLogsForParty(MessagePayload<NetworkUpdateTradeActionLogsForParty> obj)
    {
        if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.MobilePartyId, out var mobileParty)) return;

        var tradeActionLogs = new List<CaravansCampaignBehavior.TradeActionLog>();
        foreach (var tradeActionLogData in obj.What.TradeActionLogsData)
        {
            if (!UnpackTradeActionLogData(tradeActionLogData, out var tradeActionLog)) continue;

            tradeActionLogs.Add(tradeActionLog);
        }

        GameThread.Run(() =>
        {
            GetCaravansBehavior()._tradeActionLogs[mobileParty] = tradeActionLogs;
        });
    }

    private void Handle_NetworkAddToLootedCaravans(MessagePayload<NetworkAddToLootedCaravans> obj)
    {
        GameThread.RunSafe(() =>
        {
            if (!objectManager.TryGetObjectWithLogging<MobileParty>(obj.What.CaravanPartyId, out var caravanParty)) return;

            GetCaravansBehavior()._lootedCaravans.Add(caravanParty, obj.What.CampaignTime);
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
