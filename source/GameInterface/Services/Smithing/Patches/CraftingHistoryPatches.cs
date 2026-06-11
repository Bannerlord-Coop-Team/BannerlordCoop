using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Smithing.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.CraftingSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.WeaponDesign;
using TaleWorlds.Core;

namespace GameInterface.Services.Smithing.Patches
{
    [HarmonyPatch(typeof(CraftingCampaignBehavior))]
    internal class AddItemToHistoryPatch
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CraftingCampaignBehavior>();

        [HarmonyPatch("AddItemToHistory")]
        [HarmonyPrefix]
        public static bool AddItemToHistory(ref CraftingCampaignBehavior __instance, ItemObject craftedObject)
        {
            // Only run if original allowed
            return CallOriginalPolicy.IsOriginalAllowed();
        }

        public static void OverrideAddItemToHistory(ref CraftingCampaignBehavior __instance, ItemObject craftedItem)
        {
            while (__instance._cratingItemsHistory.Count >= 10)
            {
                __instance._cratingItemsHistory.RemoveAt(0);
            }
            __instance._cratingItemsHistory.Add(craftedItem);

            // Send updated history to server
            var message = new CraftedItemHistoryUpdated(Hero.MainHero, __instance._cratingItemsHistory);
            MessageBroker.Instance.Publish(__instance, message);
        }
    }

    [HarmonyPatch(typeof(CraftingHistoryVM))]
    internal class RefreshCraftingHistoryPatch
    {
        [HarmonyPatch("RefreshCraftingHistory")]
        [HarmonyPrefix]
        public static bool RefreshCraftingHistory(CraftingHistoryVM __instance)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            // Run in new AllowedThread to allow the ItemObject stringIds to be updated
            using (new AllowedThread())
            {
                __instance.FinalizeHistory();
                CraftingOrder craftingOrder = __instance._getActiveOrder();
                foreach (WeaponDesign weaponDesign in __instance._craftingBehavior.CraftingHistory)
                {
                    if (craftingOrder == null || weaponDesign.Template.TemplateName.ToString() == craftingOrder.PreCraftedWeaponDesignItem.WeaponDesign.Template.TemplateName.ToString())
                    {
                        __instance.CraftingHistory.Add(new WeaponDesignSelectorVM(weaponDesign, new Action<WeaponDesignSelectorVM>(__instance.ExecuteSelect)));
                    }
                }
                __instance.HasItemsInHistory = (__instance.CraftingHistory.Count > 0);
                __instance.ExecuteSelect(null);
            }

            return false;
        }
    }
}