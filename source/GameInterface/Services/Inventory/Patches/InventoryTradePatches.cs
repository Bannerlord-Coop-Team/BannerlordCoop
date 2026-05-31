using Common.Logging;
using Common.Messaging;
using GameInterface.Services.Inventory.Messages;
using HarmonyLib;
using Helpers;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace GameInterface.Services.Inventory.Patches
{
    [HarmonyPatch(typeof(SPInventoryVM))]
    internal class InventoryTradePatches
    {
        private static readonly ILogger Logger = LogManager.GetLogger<SPInventoryVM>();

        [HarmonyPatch(nameof(SPInventoryVM.ExecuteBuyAllItems))]
        [HarmonyPrefix]
        public static bool ExecuteBuyAllItemsPrefix(ref SPInventoryVM __instance)
        {
            if (!__instance._inventoryLogic.IsTrading) return true;

            int availableHeroGold = Hero.MainHero.Gold;
            int cost = 0;

            // Only approximate, might not be the best estimate as it doesn't factor in changing costs as items are bought
            foreach (SPItemVM itemVM in __instance.LeftItemListVM)
            {
                if (itemVM != null && !itemVM.IsFiltered && !itemVM.IsLocked && itemVM.IsTransferable)
                {
                    cost += itemVM.TotalCost * itemVM.ItemCount;
                }
            }

            if (availableHeroGold - cost < 0)
            {
                MBInformationManager.AddQuickInformation(GameTexts.FindText("str_warning_you_dont_have_enough_money", null), 0, null, null, "");
                return false;
            }

            return true;
        }

        [HarmonyPatch(nameof(SPInventoryVM.ExecuteResetTranstactions))]
        [HarmonyPrefix]
        public static bool ExecuteResetTranstactionsPrefix(ref SPInventoryVM __instance)
        {
            __instance._inventoryLogic.Reset(false);

            // Reset is disabled for live trading, don't show message
            if (!__instance._inventoryLogic.IsTrading)
            {
                InformationManager.DisplayMessage(new InformationMessage(GameTexts.FindText("str_inventory_reset_message", null).ToString()));
            }
            __instance.CurrentFocusedItem = null;

            return false;
        }

        [HarmonyPatch(nameof(SPInventoryVM.TransferAllForSettlement))]
        [HarmonyPrefix]
        public static bool TransferAllForSettlementPrefix(ref SPInventoryVM __instance)
        {
            if (!__instance._inventoryLogic.IsTrading) return true;

            int availableSettlementGold = __instance.LeftInventoryOwnerGold;
            int cost = 0;

            // Only approximate, might not be the best estimate as it doesn't factor in changing costs as items are bought
            foreach (SPItemVM itemVM in __instance.RightItemListVM)
            {
                if (itemVM != null && !itemVM.IsFiltered && !itemVM.IsLocked && itemVM.IsTransferable)
                {
                    cost += itemVM.TotalCost * itemVM.ItemCount;
                }
            }

            if (availableSettlementGold - cost < 0)
            {
                //MBInformationManager.AddQuickInformation(GameTexts.FindText("str_trader_doesnt_have_enough_money", null), 0, null, null, "");
                InformationManager.ShowInquiry(new InquiryData("", GameTexts.FindText("str_trader_doesnt_have_enough_money", null).ToString(), false, true, GameTexts.FindText("str_yes", null).ToString(), GameTexts.FindText("str_no", null).ToString(), delegate ()
                {
                    InventoryScreenHelper.CloseScreen(false);
                }, null, "", 0f, null, null, null), false, false);
                return false;
            }

            return true;
        }
    }
}
