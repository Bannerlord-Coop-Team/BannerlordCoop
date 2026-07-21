using GameInterface.Extentions;
using GameInterface.Services.Clans.Extensions;
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
        private static bool IsHumanPlayerCharacterPrefix(Hero __instance, ref bool __result)
        {
            __result = Campaign.Current.CampaignObjectManager.GetPlayerMobileParties().Any(party => party.LeaderHero == __instance);
            return false;
        }

        [HarmonyPatch(nameof(Hero.IsPlayerCompanion), MethodType.Getter)]
        [HarmonyPrefix]
        private static bool IsPlayerCompanionPrefix(Hero __instance, ref bool __result)
        {
            __result = __instance.CompanionOf != null && __instance.CompanionOf.IsPlayerClan();
            return false;
        }

        [HarmonyPatch("OnLoad")]
        [HarmonyPostfix]
        private static void OnLoadPostfix(Hero __instance)
        {
            if (__instance.CharacterObject != null) return;
            if (!ContainerProvider.TryResolve<IHeroCharacterObjectRepairer>(out var repairer)) return;

            repairer.TryRepair(__instance);
        }
    }
}
