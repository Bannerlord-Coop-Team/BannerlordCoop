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
    internal class ChangeSkillLevelPatch
    {
        private static readonly ILogger Logger = LogManager.GetLogger<HeroDeveloper>();

        [HarmonyPatch(nameof(HeroDeveloper.ChangeSkillLevelFromXpChange))]
        [HarmonyPrefix]
        public static bool ChangeSkillLevelFromXpChangePrefix(ref HeroDeveloper __instance, SkillObject skill, int changeAmount, bool shouldNotify = false)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            // Don't allow clients to change skill level
            if (ModInformation.IsClient) return false;

            return true;
        }
    }
}
