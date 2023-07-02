using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements.Buildings;

namespace GameInterface.Services.Buildings.Patches
{
    /// <summary>
    /// Disables functionality for <see cref="Building"/>
    /// </summary>
    [HarmonyPatch(typeof(Building))]
    internal class BuildingPatches
    {
        [HarmonyPatch(nameof(Building.CurrentLevel), MethodType.Setter)]
        [HarmonyPrefix]
        private static bool CurrentLevelPrefix()
        {
            return false;
        }

        [HarmonyPatch(nameof(Building.LevelUp))]
        [HarmonyPrefix]
        private static bool LevelUpPrefix()
        {
            return false;
        }

        [HarmonyPatch(nameof(Building.LevelDown))]
        [HarmonyPrefix]
        private static bool LevelDownPrefix()
        {
            return false;
        }

        [HarmonyPatch(nameof(Building.HitPointChanged))]
        [HarmonyPrefix]
        private static bool HitPointChangedPrefix()
        {
            return false;
        }
    }
}
