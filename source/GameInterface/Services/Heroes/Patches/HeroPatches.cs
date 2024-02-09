using GameInterface.Extentions;
using HarmonyLib;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Heroes.Patches
{
    /// <summary>
    /// Patch for Hero class methods.
    /// </summary>
    [HarmonyPatch(typeof(Hero))]
    public class HeroPatches
    {
        /// <summary>
        /// Patch for determining whether a Hero is a player's hero or not.
        /// </summary>
        /// <param name="__instance">hero instance</param>
        /// <param name="__result">result</param>
        /// <returns></returns>
        [HarmonyPrefix]
        [HarmonyPatch(nameof(Hero.IsHumanPlayerCharacter),MethodType.Getter)]
        private static bool IsHumanPlayerCharacterPrefix(Hero __instance, bool __result)
        {
            __result = Campaign.Current.CampaignObjectManager.GetPlayerMobileParties().Any(party => party.LeaderHero == __instance);
            return false;
        }

    }
}
