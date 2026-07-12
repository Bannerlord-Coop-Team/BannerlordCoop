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

        [HarmonyPatch("SetSkillXp")]
        [HarmonyPrefix]
        public static bool SetSkillXp(ref HeroDeveloper __instance, PropertyObject skill, float value)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            SkillObject skillObject = (SkillObject) skill;

            // Publish message with data
            var message = new SkillXpSet(__instance, skillObject, value);
            if (!HeroDeveloperBatchScope.TryEnqueue(message))
            {
                MessageBroker.Instance.Publish(__instance, message);
            }

            // Server-owned campaign logic remains live; clients wait for the authoritative replay.
            return ModInformation.IsServer;
        }
    }
}
