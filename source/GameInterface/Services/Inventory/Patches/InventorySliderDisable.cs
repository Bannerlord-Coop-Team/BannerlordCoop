using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Inventory;

namespace GameInterface.Services.Inventory.Patches
{
    [HarmonyPatch(typeof(InventoryTradeVM))]
    internal class InventoryTradeVMPatches
    {
        [HarmonyPatch(nameof(InventoryTradeVM.ThisStockUpdated))]
        [HarmonyPrefix]
        public static bool ThisStockUpdatedPrefix(InventoryTradeVM __instance)
        {
            // Button to handle this instead?
            //__instance.ExecuteApplyTransaction();
            __instance.OtherStock = __instance.TotalStock - __instance.ThisStock;
            __instance.IsThisStockIncreasable = (__instance.OtherStock > 0);
            __instance.IsOtherStockIncreasable = (__instance.OtherStock < __instance.TotalStock);
            __instance.UpdateProperties();

            return false;
        }
    }
}
