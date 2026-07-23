using HarmonyLib;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Tests.Bootstrap.Patches
{
    [HarmonyPatch(typeof(Hero))]
    internal class HeroPatches
    {
        [HarmonyPatch(nameof(Hero.ChangeState))]
        [HarmonyPrefix]
        private static bool ChangeStatePatch(ref Hero __instance, ref Hero.CharacterStates newState)
        {
            __instance._heroState = newState;
            return false;
        }
    }
}
