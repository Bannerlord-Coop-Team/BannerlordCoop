using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Towns.Patches
{
    /// <summary>
    /// Disables all functionality for Town
    /// </summary>
    [HarmonyPatch(typeof(Town))]
    internal class TownPatches
    {
        [HarmonyPatch("DailyTick")]
        [HarmonyPrefix]
        private static bool DailyTickPrefix()
        {
            return false;
        }

        [HarmonyPatch(nameof(Town.Security), MethodType.Setter)]
        [HarmonyPrefix]
        private static bool SecurityPrefix()
        {
            return false;
        }

        [HarmonyPatch(nameof(Town.Loyalty), MethodType.Setter)]
        [HarmonyPrefix]
        private static bool LoyaltyPrefix()
        {
            return false;
        }


        [HarmonyPatch(nameof(Town.TradeTaxAccumulated), MethodType.Setter)]
        [HarmonyPrefix]
        private static bool TradeTaxAccumulatedPrefix()
        {
            return false;
        }

        [HarmonyPatch(nameof(Town.Governor), MethodType.Setter)]
        [HarmonyPrefix]
        private static bool GovernorPrefix()
        {
            return false;
        }

        [HarmonyPatch(nameof(Town.LastCapturedBy), MethodType.Setter)]
        [HarmonyPrefix]
        private static bool LastCapturedByPrefix()
        {
            return false;
        }
    }
}
