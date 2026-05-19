using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.Smithing.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.ViewModelCollection.Order;

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

    [HarmonyPatch(typeof(ItemObject))]
    internal class InitCraftedItemObjectPatch
    {
        private static readonly ILogger Logger = LogManager.GetLogger<ItemObject>();

        [HarmonyPatch("InitCraftedItemObject")]
        [HarmonyPostfix]
        public static void InitCraftedItemObject(ref ItemObject __instance, TextObject name, BasicCultureObject culture, ItemFlags itemProperties, float weight, float appearance, WeaponDesign craftedData, ItemObject.ItemTypeEnum itemType)
        {
            if (ModInformation.IsServer)
            {
                Logger.Information("InitCraftedItemObject called on server");
            }
            else
            {
                Logger.Information("InitCraftedItemObject called on client");
            }
        }
    }
}