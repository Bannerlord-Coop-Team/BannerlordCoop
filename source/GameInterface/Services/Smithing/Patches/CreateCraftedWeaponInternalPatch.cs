using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Smithing.Messages;
using HarmonyLib;
using Serilog;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

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
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            string nextCraftedItemId = __instance.GetNextCraftedItemId();
            ItemObject craftedItemObject;
            using (new AllowedThread())
            {
                craftedItemObject = (GameStateManager.Current.ActiveState as CraftingState).CraftingLogic.GetCurrentCraftedItemObject(true, nextCraftedItemId);
                ItemObject.InitAsPlayerCraftedItem(ref craftedItemObject);

                // May need to replace this if causes issues, uses MBObjectManager
                ItemObject registeredObject = MBObjectManager.Instance.RegisterObject<ItemObject>(craftedItemObject);

                CampaignEventDispatcher.Instance.OnNewItemCrafted(craftedItemObject, weaponModifier, !isFreeMode);
            }
            Crafting craftingLogic = (GameStateManager.Current.ActiveState as CraftingState).CraftingLogic;

            // Publish message with data
            var message = new CraftedWeaponInternallyCreated(__instance, isFreeMode, crafterHero, craftedItemObject, weaponDesign, weaponModifier, nextCraftedItemId, Hero.MainHero, craftingLogic);
            MessageBroker.Instance.Publish(__instance, message);

            // Need to return the ItemObject for client's CraftingVM
            __result = craftedItemObject;

            // Patched separately for sending to server
            __instance.AddResearchPoints(weaponDesign.Template, Campaign.Current.Models.SmithingModel.GetPartResearchGainForSmithingItem(craftedItemObject, crafterHero, isFreeMode));

            // Skip original to override original client saving
            return false;
        }
    }
}