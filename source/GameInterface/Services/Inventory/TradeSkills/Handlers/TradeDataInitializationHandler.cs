using Common;
using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Inventory.TradeSkills.Interfaces;
using GameInterface.Services.Inventory.TradeSkills.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Inventory.TradeSkills.Handlers;

internal class TradeDataInitializationHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<TradeDataInitializationHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ISessionTradePlayerDataInterface sessionTradePlayerDataInterface;

    private TradePlayerData tradePlayerData;

    public TradeDataInitializationHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ISessionTradePlayerDataInterface sessionTradePlayerDataInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.sessionTradePlayerDataInterface = sessionTradePlayerDataInterface;

        messageBroker.Subscribe<InitializeClientTradeData>(Handle);
        messageBroker.Subscribe<PlayerHeroChanged>(Handle);
        messageBroker.Subscribe<NetworkInitializeServerTradeDataKeys>(Handle);
    }

    public void Dispose()
    {
        messageBroker.Unsubscribe<InitializeClientTradeData>(Handle);
        messageBroker.Unsubscribe<PlayerHeroChanged>(Handle);
        messageBroker.Unsubscribe<NetworkInitializeServerTradeDataKeys>(Handle);
    }

    private void Handle(MessagePayload<InitializeClientTradeData> obj)
    {
        tradePlayerData = obj.What.TradePlayerData;
    }

    // Need to load trade data when the hero changes for the player
    private void Handle(MessagePayload<PlayerHeroChanged> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.NewHero, out string playerHeroId)) return;

        TradeSkillCampaignBehavior tradeSkillCampaignBehavior = Campaign.Current.GetCampaignBehavior<TradeSkillCampaignBehavior>();
        TradeRumorsCampaignBehavior tradeRumorsCampaignBehavior = Campaign.Current.GetCampaignBehavior<TradeRumorsCampaignBehavior>();

        tradeSkillCampaignBehavior.ItemsTradeData = GetItemsTradeData(playerHeroId);
        tradeRumorsCampaignBehavior._tradeRumors = GetTradeRumors(playerHeroId);
        tradeRumorsCampaignBehavior._enteredSettlements = GetEnteredSettlements(playerHeroId);

        LoadSettlementBribePaidData(playerHeroId);

        network.SendAll(new NetworkInitializeServerTradeDataKeys(playerHeroId));
    }

    private void Handle(MessagePayload<NetworkInitializeServerTradeDataKeys> obj)
    {
        GameThread.RunSafe(() =>
        {
            sessionTradePlayerDataInterface.AddPlayerKeys(obj.What.PlayerHeroId);
        });
    }

    private Dictionary<ItemObject, TradeSkillCampaignBehavior.ItemTradeData> GetItemsTradeData(string playerHeroId)
    {
        var itemsTradeData = new Dictionary<ItemObject, TradeSkillCampaignBehavior.ItemTradeData>();

        // Null and key check for players without existing trade data
        if (tradePlayerData?.PlayerItemsTradeData?.ContainsKey(playerHeroId) != true) return itemsTradeData;

        foreach (var itemIdData in tradePlayerData.PlayerItemsTradeData[playerHeroId])
        {
            if (!objectManager.TryGetObjectWithLogging<ItemObject>(itemIdData.Key, out var item)) continue;

            itemsTradeData[item] = new TradeSkillCampaignBehavior.ItemTradeData(itemIdData.Value.Item1, itemIdData.Value.Item2);
        }

        return itemsTradeData;
    }

    private List<TradeRumor> GetTradeRumors(string playerHeroId)
    {
        var tradeRumors = new List<TradeRumor>();

        // Null and key check for players without existing trade rumors data
        if (tradePlayerData?.PlayerTradeRumors?.ContainsKey(playerHeroId) != true) return tradeRumors;

        foreach (var tradeRumorData in tradePlayerData.PlayerTradeRumors[playerHeroId])
        {
            if (!objectManager.TryGetObjectWithLogging<Settlement>(tradeRumorData.SettlementId, out var settlement)) continue;
            if (!objectManager.TryGetObjectWithLogging<ItemObject>(tradeRumorData.ItemObjectId, out var itemObject)) continue;

            var tradeRumor = new TradeRumor(settlement, itemObject, tradeRumorData.BuyPrice, tradeRumorData.SellPrice, 0)
            {
                RumorEndTime = new CampaignTime(tradeRumorData.RumorEndTime)
            };

            tradeRumors.Add(tradeRumor);
        }

        return tradeRumors;
    }

    private Dictionary<Settlement, CampaignTime> GetEnteredSettlements(string playerHeroId)
    {
        var enteredSettlements = new Dictionary<Settlement, CampaignTime>();

        // Null and key check for players without existing entered settlements data
        if (tradePlayerData?.PlayerEnteredSettlements?.ContainsKey(playerHeroId) != true) return enteredSettlements;

        foreach (var enteredSettlementData in tradePlayerData.PlayerEnteredSettlements[playerHeroId])
        {
            if (!objectManager.TryGetObjectWithLogging<Settlement>(enteredSettlementData.Key, out var settlement)) continue;

            enteredSettlements.Add(settlement, new CampaignTime(enteredSettlementData.Value));
        }

        return enteredSettlements;
    }

    private void LoadSettlementBribePaidData(string playerHeroId)
    {
        // Null and key check for players without existing bribe paid data
        if (tradePlayerData?.PlayerSettlementBribePaid?.ContainsKey(playerHeroId) != true) return;

        foreach (var settlementBribePaid in tradePlayerData.PlayerSettlementBribePaid[playerHeroId])
        {
            if (!objectManager.TryGetObjectWithLogging<Settlement>(settlementBribePaid.Key, out var settlement)) continue;

            settlement.BribePaid = settlementBribePaid.Value;
        }
    }
}
