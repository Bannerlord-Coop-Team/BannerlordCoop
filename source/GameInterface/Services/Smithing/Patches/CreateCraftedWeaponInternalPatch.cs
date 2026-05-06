using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Smithing.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;

namespace GameInterface.Services.Smithing.Patches
{
    [HarmonyPatch(typeof(CraftingCampaignBehavior))]
    internal class CreateCraftedWeaponInternalPatch
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CraftingCampaignBehavior>();

        [HarmonyPatch("CreateCraftedWeaponInternal")]
        [HarmonyPrefix]
        public static bool CreateCraftedWeaponInternal(ref CraftingCampaignBehavior __instance, ref ItemObject __result, bool isFreeMode, Hero crafterHero, WeaponDesign weaponDesign, ItemModifier weaponModifier = null)
        {
            string nextCraftedItemId = __instance.GetNextCraftedItemId();

            ItemObject craftedItemObject;
            using (new AllowedThread())
            {
                craftedItemObject = (GameStateManager.Current.ActiveState as CraftingState).CraftingLogic.GetCurrentCraftedItemObject(true, nextCraftedItemId);
                craftedItemObject.StringId = nextCraftedItemId;
                ItemObject.InitAsPlayerCraftedItem(ref craftedItemObject);
                CampaignEventDispatcher.Instance.OnNewItemCrafted(craftedItemObject, weaponModifier, !isFreeMode);
            }

            // Publish message with data
            var message = new CraftedWeaponInternallyCreated(__instance, isFreeMode, crafterHero, craftedItemObject, weaponDesign, weaponModifier, nextCraftedItemId);
            MessageBroker.Instance.Publish(__instance, message);

            // Need to return the ItemObject for client's CraftingVM
            __result = craftedItemObject;

            // Skip original to override original client saving
            return false;
        }

        [HarmonyPatch("CreateCraftedWeaponInternal")]
        [HarmonyPostfix]
        public static void Postfix()
        {
            //AllowedThread.RevokeThisThread();
        }
    }
}