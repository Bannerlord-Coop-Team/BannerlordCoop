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
            __instance.Value = ApplyMultiplier(__instance.Value, multiplier);
        }
    }

    internal static int ApplyMultiplier(int value, float multiplier)
    {
        if (multiplier < 0 || float.IsNaN(multiplier) || float.IsInfinity(multiplier)) return value;

        var scaledValue = value * (double)multiplier;
        if (scaledValue > int.MaxValue) return int.MaxValue;
        if (scaledValue < int.MinValue) return int.MinValue;

        return (int)scaledValue;
    }
}
