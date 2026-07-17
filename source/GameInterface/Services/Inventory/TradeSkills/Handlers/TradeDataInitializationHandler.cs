using Common.Logging;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Heroes.Messages;
using GameInterface.Services.Inventory.TradeSkills.Interfaces;
using GameInterface.Services.Inventory.TradeSkills.Messages;
using GameInterface.Services.MobileParties;
using GameInterface.Services.MobileParties.Handlers;
using GameInterface.Services.MobileParties.Interfaces;
using GameInterface.Services.MobileParties.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Inventory.TradeSkills.Handlers;

internal class TradeDataInitializationHandler : IHandler
{
    private static readonly ILogger Logger = LogManager.GetLogger<TradeDataInitializationHandler>();

    private readonly IMessageBroker messageBroker;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private readonly ITradeSkillCampaignBehaviorInterface tradeSkillCampaignBehaviorInterface;

    private TradePlayerData tradePlayerData;

    public TradeDataInitializationHandler(
        IMessageBroker messageBroker,
        IObjectManager objectManager,
        INetwork network,
        ISessionInteractionsPlayerDataInterface sessionInteractionsPlayerDataInterface,
        ITradeSkillCampaignBehaviorInterface tradeSkillCampaignBehaviorInterface)
    {
        this.messageBroker = messageBroker;
        this.objectManager = objectManager;
        this.network = network;
        this.tradeSkillCampaignBehaviorInterface = tradeSkillCampaignBehaviorInterface;

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

    // Need to load interactions data when the hero changes for the player
    private void Handle(MessagePayload<PlayerHeroChanged> obj)
    {
        if (!objectManager.TryGetIdWithLogging(obj.What.NewHero, out string playerHeroId)) return;

        TradeSkillCampaignBehavior tradeSkillCampaignBehavior = Campaign.Current.GetCampaignBehavior<TradeSkillCampaignBehavior>();

        tradeSkillCampaignBehavior.ItemsTradeData = GetItemsTradeData(playerHeroId);

        network.SendAll(new NetworkInitializeServerTradeDataKeys(playerHeroId));
    }

    private void Handle(MessagePayload<NetworkInitializeServerTradeDataKeys> obj)
    {
        tradeSkillCampaignBehaviorInterface.AddPlayerKeys(obj.What.PlayerHeroId);
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
}
