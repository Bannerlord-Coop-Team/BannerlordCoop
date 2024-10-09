using HarmonyLib;
using Helpers;

namespace GameInterface.Services.CraftingService.Patches
{
    /// <summary>
    /// Disables functionality for opening the crafting interface
    /// </summary>
    [HarmonyPatch(typeof(CraftingHelper))]
    internal class CraftingPatches
    {
        [HarmonyPatch(nameof(CraftingHelper.OpenCrafting))]
        [HarmonyPrefix]
        private static bool OpenCraftingPrefix()
        {
            return false;
        }
    }
}
