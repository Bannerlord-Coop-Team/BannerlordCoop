using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Extentions;
using GameInterface.Policies;
using GameInterface.Services.Clans.Extensions;
using GameInterface.Services.Heroes.Messages;
using HarmonyLib;
using Serilog;
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
        private static readonly ILogger Logger = LogManager.GetLogger<HeroPatches>();

        [HarmonyPatch(nameof(Hero.ChangeState))]
        [HarmonyPrefix]
        private static void ChangeStatePrefix(Hero __instance, Hero.CharacterStates newState)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return;
            if (ModInformation.IsClient)
            {
                Logger.Error("Client updated managed {var}", nameof(Hero._heroState));
                return;
            }

            MessageBroker.Instance.Publish(__instance, new HeroStateChanged((int)newState, __instance));
        }

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
            if (!ContainerProvider.TryResolve<IDeadHeroCaptivityRepairer>(out var repairer)) return;

            repairer.TryRestoreDeadState(__instance);
        }
    }
}
