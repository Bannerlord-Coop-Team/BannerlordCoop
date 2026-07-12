using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.HeroDevelopers.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace GameInterface.Services.HeroDevelopers.Patches
{
    [HarmonyPatch(typeof(HeroDeveloper))]
    internal class SetSkillXpPatch
    {
        private static readonly ILogger Logger = LogManager.GetLogger<HeroDeveloper>();

        [HarmonyPatch(nameof(HeroDeveloper.SetSkillXp))]
        [HarmonyPrefix]
        public static bool SetSkillXpPrefix(ref HeroDeveloper __instance, PropertyObject skill, float value)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            // Don't allow clients to change skill xp
            if (ModInformation.IsClient) return false;

            return true;
        }
    }
}
