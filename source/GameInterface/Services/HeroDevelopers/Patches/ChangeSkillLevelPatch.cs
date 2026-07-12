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

        [HarmonyPatch("ChangeSkillLevelFromXpChange")]
        [HarmonyPrefix]
        public static bool ChangeSkillLevelFromXpChange(ref HeroDeveloper __instance, SkillObject skill, int changeAmount, bool shouldNotify = false)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            // Publish message with data
            var message = new SkillLevelChange(__instance, skill, changeAmount, shouldNotify);
            if (!HeroDeveloperBatchScope.TryEnqueue(message))
            {
                MessageBroker.Instance.Publish(__instance, message);
            }

            // Skip original to override original client saving
            return false;
        }
    }
}
