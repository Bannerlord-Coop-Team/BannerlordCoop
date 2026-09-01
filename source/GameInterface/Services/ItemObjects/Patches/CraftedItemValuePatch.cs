using GameInterface.Configuration;
using HarmonyLib;
using TaleWorlds.Core;

namespace GameInterface.Services.ItemObjects.Patches;

[HarmonyPatch(typeof(ItemObject))]
internal class CraftedItemValuePatch
{
    [HarmonyPatch(nameof(ItemObject.DetermineValue))]
    [HarmonyPostfix]
    public static void DetermineValuePostfix(ItemObject __instance)
    {
        if (__instance.IsCraftedByPlayer)
        {
            var multiplier = ModConfigProvider.ModOptions.SmithingCraftedItemsValueMultiplier;
            if (multiplier < 0 || multiplier == float.PositiveInfinity || multiplier == float.NaN) multiplier = 1;

            __instance.Value = (int)(__instance.Value * multiplier);
        }
    }
}
