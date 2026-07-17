using Common.Logging;
using Common.Network;
using GameInterface.CoopSessionData;
using GameInterface.Services.Inventory.TradeSkills.Data;
using GameInterface.Services.Inventory.TradeSkills.Messages;
using GameInterface.Services.ObjectManager;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using static TaleWorlds.CampaignSystem.CampaignBehaviors.TradeSkillCampaignBehavior;

namespace GameInterface.Services.Inventory.TradeSkills.Interfaces;

public interface ITradeSkillCampaignBehaviorInterface : IGameAbstraction
{
    void UpdatePlayerInventory(Hero playerHero, List<ValueTuple<ItemRosterElement, int>> purchasedItems, List<ValueTuple<ItemRosterElement, int>> soldItems, bool isTrading);
    bool TryGetTradeSkillBehavior(out TradeSkillCampaignBehavior tradeSkillBehavior);
    void AddPlayerKeys(string playerHeroId);
}

public class TradeSkillCampaignBehaviorInterface : ITradeSkillCampaignBehaviorInterface
{
    private static readonly ILogger Logger = LogManager.GetLogger<TradeSkillCampaignBehaviorInterface>();

    private ICoopSessionProvider coopSessionProvider;
    private readonly IObjectManager objectManager;
    private readonly INetwork network;
    private TradePlayerData TradePlayerData => coopSessionProvider.CoopSession.TradePlayerData;

    public TradeSkillCampaignBehaviorInterface(ICoopSessionProvider coopSessionProvider, IObjectManager objectManager, INetwork network)
    {
        this.coopSessionProvider = coopSessionProvider;
        this.objectManager = objectManager;
        this.network = network;
    }

    /// <summary>
    /// Replacement for TradeSkillCampaignBehavior.PlayerInventoryUpdated to work for multiple players
    /// TradeData is saved in CoopSession on server and updated locally on target client
    /// </summary>
    public void UpdatePlayerInventory(Hero playerHero, List<ValueTuple<ItemRosterElement, int>> purchasedItems, List<ValueTuple<ItemRosterElement, int>> soldItems, bool isTrading)
    {
        if (!objectManager.TryGetIdWithLogging(playerHero, out var playerHeroId)) return;

        int gainedXp = 0;
        if (isTrading)
        {
            foreach (ValueTuple<ItemRosterElement, int> valueTuple in purchasedItems)
            {
                ProcessPurchases(playerHeroId, valueTuple.Item1, valueTuple.Item2);
            }
        }
        foreach (ValueTuple<ItemRosterElement, int> valueTuple2 in soldItems)
        {
            gainedXp += ProcessSales(playerHeroId, playerHero.PartyBelongedTo, valueTuple2.Item1, valueTuple2.Item2, isTrading);
        }
        
        if (isTrading)
        {
            SkillLevelingManager.OnTradeProfitMade(playerHero.PartyBelongedTo.Party, gainedXp);

            // Appears to be unimplemented in TaleWorlds code
            //CampaignEventDispatcher.Instance.OnPlayerTradeProfit(gainedXp);
        }

        // Update client trade data
        network.SendAll(new NetworkUpdateTradeData(playerHeroId, purchasedItems, soldItems, isTrading));
    }

    /// <summary>
    /// Re-implement TradeSkillCampaignBehavior.ProcessPurchases to instead save in CoopSession on server
    /// </summary>
    private void ProcessPurchases(string playerHeroId, ItemRosterElement itemRosterElement, int totalPrice)
    {
        if (itemRosterElement.EquipmentElement.ItemModifier != null) return;
        if (!objectManager.TryGetIdWithLogging(itemRosterElement.EquipmentElement.Item, out var itemId)) return;

        if (!TradePlayerData.PlayerItemsTradeData[playerHeroId].TryGetValue(itemId, out Tuple<float, int> itemTradeData))
        {
            itemTradeData = new Tuple<float, int>(0, 0);
        }
        int num = itemTradeData.Item2 + itemRosterElement.Amount;
        float averagePrice = (itemTradeData.Item1 * (float)itemTradeData.Item2 + (float)totalPrice) / MathF.Max(0.0001f, (float)num);
        TradePlayerData.PlayerItemsTradeData[playerHeroId][itemId] = new Tuple<float, int>(averagePrice, num);
    }

    /// <summary>
    /// Re-implement TradeSkillCampaignBehavior.ProcessSales to instead save in CoopSession on server
    /// </summary>
    private int ProcessSales(string playerHeroId, MobileParty playerParty, ItemRosterElement itemRosterElement, int totalPrice, bool isTrading)
    {
        if (itemRosterElement.EquipmentElement.ItemModifier != null) return 0;
        if (!objectManager.TryGetIdWithLogging(itemRosterElement.EquipmentElement.Item, out var itemId)) return 0;

        int result = 0;
        if (TradePlayerData.PlayerItemsTradeData[playerHeroId].TryGetValue(itemId, out Tuple<float, int> itemTradeData))
        {
            if (isTrading)
            {
                int num = MathF.Min(itemTradeData.Item2, itemRosterElement.Amount);
                int num2 = itemTradeData.Item2 - num;
                float f = (float)num * itemTradeData.Item1;
                float num3 = (float)totalPrice / MathF.Max(0.001f, (float)itemRosterElement.Amount);
                int num4 = MathF.Round((float)num * num3);
                result = MathF.Max(0, num4 - MathF.Floor(f));
                if (num2 == 0)
                {
                    TradePlayerData.PlayerItemsTradeData[playerHeroId].Remove(itemId);
                }
                else
                {
                    TradePlayerData.PlayerItemsTradeData[playerHeroId][itemId] = new Tuple<float, int>(itemTradeData.Item1, num2);
                }
            }
            else
            {
                int num5 = playerParty.ItemRoster.FindIndexOfElement(itemRosterElement.EquipmentElement);
                if (num5 == -1)
                {
                    TradePlayerData.PlayerItemsTradeData[playerHeroId].Remove(itemId);
                }
                else
                {
                    int amount = playerParty.ItemRoster.GetElementCopyAtIndex(num5).Amount;
                    if (itemTradeData.Item2 > amount)
                    {
                        TradePlayerData.PlayerItemsTradeData[playerHeroId][itemId] = new Tuple<float, int>(itemTradeData.Item1, amount);
                    }
                }
            }
        }
        return result;
    }

    public bool TryGetTradeSkillBehavior(out TradeSkillCampaignBehavior tradeSkillBehavior)
    {
        tradeSkillBehavior = Campaign.Current?.GetCampaignBehavior<TradeSkillCampaignBehavior>();
        if (tradeSkillBehavior != null) return true;

        Logger.Debug("Skipping trade skill update because the campaign behavior is unavailable");
        return false;
    }

    public void AddPlayerKeys(string playerHeroId)
    {
        if (TradePlayerData == null)
        {
            Logger.Error("TradePlayerData was null");
            return;
        }

        if (!TradePlayerData.PlayerItemsTradeData.ContainsKey(playerHeroId))
        {
            TradePlayerData.PlayerItemsTradeData[playerHeroId] = new Dictionary<string, Tuple<float, int>>();
        }
    }
}