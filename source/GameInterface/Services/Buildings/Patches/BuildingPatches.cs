using HarmonyLib;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using GameInterface.Policies;

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
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;
            return false;
        }

        [HarmonyPatch(nameof(Building.LevelUp))]
        [HarmonyPrefix]
        private static bool LevelUpPrefix()
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;
            return false;
        }

        [HarmonyPatch(nameof(Building.LevelDown))]
        [HarmonyPrefix]
        private static bool LevelDownPrefix()
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;
            return false;
        }

        [HarmonyPatch(nameof(Building.HitPointChanged))]
        [HarmonyPrefix]
        private static bool HitPointChangedPrefix()
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;
            return false;
        }
    }
}
