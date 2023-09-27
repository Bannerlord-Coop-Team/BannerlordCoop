using HarmonyLib;
using Helpers;
using System.Reflection;
using TaleWorlds.CampaignSystem.GameComponents;
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
    }

    [HarmonyPatch(typeof(DefaultSettlementProsperityModel))]
    internal class DefaultSettlementProsperityModelPatches
    {
        private static readonly MethodInfo CalculateProsperityChange = typeof(DefaultSettlementProsperityModel).GetMethod("CalculateProsperityChangeInternal", BindingFlags.NonPublic | BindingFlags.Instance);

        [HarmonyPatch(nameof(CalculateProsperityChange))]
        [HarmonyPrefix]
        private static bool CalculateProsperityChangePatch()
        {
            return false;
        }
    }

    [HarmonyPatch(typeof(BuildingHelper))]
    internal class BuilderHelperPatches
    {
        [HarmonyPatch("AddDefaultDailyBonus")]
        [HarmonyPrefix]
        private static bool AddDefaultDailyBonusPatch()
        {
            return false;
        }
    }
}
