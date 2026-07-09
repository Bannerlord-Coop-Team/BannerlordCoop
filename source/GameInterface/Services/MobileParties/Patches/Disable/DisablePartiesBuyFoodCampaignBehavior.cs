using Common;
using GameInterface.Services.MobileParties.Extensions;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.MobileParties.Patches.Disable;

[HarmonyPatch(typeof(PartiesBuyFoodCampaignBehavior))]
internal class DisablePartiesBuyFoodCampaignBehavior
{
    [HarmonyPatch(nameof(PartiesBuyFoodCampaignBehavior.RegisterEvents))]
    static bool Prefix() => ModInformation.IsServer;
}

[HarmonyPatch(typeof(PartiesBuyFoodCampaignBehavior))]
internal class PartiesBuyFoodCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(PartiesBuyFoodCampaignBehavior.BuyFoodInternal))]
    [HarmonyPrefix]
    public static bool BuyFoodInternalPrefix(PartiesBuyFoodCampaignBehavior __instance, MobileParty mobileParty, Settlement settlement, int numberOfFoodItemsNeededToBuy)
    {
        if (numberOfFoodItemsNeededToBuy <= 0 || mobileParty == null || mobileParty.IsPlayerParty()) return false;

        // TaleWorlds' implementation is horribly inefficient and causes severe lag for clients.
        // This is a replacement that doesn't use so many individual transactions.
        // A downside to doing this is the changing price of items per transaction isn't accounted for, which could cause parties to spend slightly more than they would in vanilla
        while (numberOfFoodItemsNeededToBuy > 0)
        {
            Campaign.Current.Models.PartyFoodBuyingModel.FindItemToBuy(mobileParty, settlement, out ItemRosterElement item, out float price);

            ItemObject itemObject = item.EquipmentElement.Item;
            if (itemObject == null) break;

            if (price > mobileParty.PartyTradeGold) break;

            int available = item.Amount;
            if (available <= 0) break;

            int foodPerItem = 1;
            if (itemObject.HasHorseComponent && itemObject.HorseComponent.IsLiveStock)
            {
                foodPerItem = itemObject.HorseComponent.MeatCount;
            }

            int maxAffordable = (int)(mobileParty.PartyTradeGold / price);
            int amountToBuy = Math.Min(available, Math.Min(maxAffordable, (numberOfFoodItemsNeededToBuy + foodPerItem - 1) / foodPerItem));

            if (amountToBuy <= 0) break;

            SellItemsAction.Apply(settlement.Party, mobileParty.Party, item, amountToBuy, null);

            numberOfFoodItemsNeededToBuy -= amountToBuy * foodPerItem;
        }

        return false;
    }
}