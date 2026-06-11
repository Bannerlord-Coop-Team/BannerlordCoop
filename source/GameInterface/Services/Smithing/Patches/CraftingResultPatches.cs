using Common.Logging;
using GameInterface.Policies;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.WeaponCrafting.WeaponDesign;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Smithing.Patches
{
    [HarmonyPatch(typeof(WeaponDesignVM))]
    internal class CraftingResultPatches
    {
        private static readonly ILogger Logger = LogManager.GetLogger<WeaponDesignVM>();

        [HarmonyPatch("CreateCraftingResultPopup")]
        [HarmonyPostfix]
        public static void CreateCraftingResultPopupPostfix(ref WeaponDesignVM __instance)
        { 
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return;

            // Need to send CompleteOrder here instead of in ExecuteFinalizeCrafting to prevent two clients completing the same order
            if (__instance.IsInOrderMode)
            {
                __instance._craftingBehavior.CompleteOrder(Settlement.CurrentSettlement.Town, __instance.ActiveCraftingOrder.CraftingOrder, __instance.CraftedItemObject, __instance._getCurrentCraftingHero().Hero);
            }
        }

        [HarmonyPatch("ExecuteFinalizeCrafting")]
        [HarmonyPrefix]
        public static bool ExecuteFinalizeCrafting(ref WeaponDesignVM __instance)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            if (__instance._craftingBehavior != null && Campaign.Current.GameMode == CampaignGameMode.Campaign)
            {
                if (GameStateManager.Current.ActiveState is CraftingState)
                {
                    // CompleteOrder and refreshing handled elsewhere
                    if (__instance.IsInOrderMode)
                    {
                        __instance.CraftedItemObject = null;
                    }
                    else
                    {
                        int bladeSize = __instance.BladeSize;
                        int guardSize = __instance.GuardSize;
                        int handleSize = __instance.HandleSize;
                        int pommelSize = __instance.PommelSize;
                        __instance.RefreshWeaponDesignMode(null, __instance._selectedWeaponClassIndex, false);
                        __instance.BladeSize = bladeSize;
                        __instance.GuardSize = guardSize;
                        __instance.HandleSize = handleSize;
                        __instance.PommelSize = pommelSize;
                    }
                }
                __instance.IsInFinalCraftingStage = false;
            }
            TextObject textObject = new TextObject("{=uZhHh7pm}Crafted {CURR_TEMPLATE_NAME}", null);
            textObject.SetTextVariable("CURR_TEMPLATE_NAME", __instance._crafting.CurrentCraftingTemplate.TemplateName);
            __instance._crafting.SetCraftedWeaponName(textObject);

            // Replace original
            return false;
        }
    }
}