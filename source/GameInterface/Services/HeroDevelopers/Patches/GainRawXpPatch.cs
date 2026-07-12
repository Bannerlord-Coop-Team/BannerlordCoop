using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.HeroDevelopers.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace GameInterface.Services.HeroDevelopers.Patches
{
    [HarmonyPatch(typeof(HeroDeveloper))]
    internal class GainRawXpPatch
    {
        private static readonly ILogger Logger = LogManager.GetLogger<HeroDeveloper>();

        [HarmonyPatch("GainRawXp")]
        [HarmonyPrefix]
        public static bool GainRawXp(ref HeroDeveloper __instance, float rawXp, ref bool shouldNotify)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            // Publish message with data
            var message = new RawXpGain(__instance, rawXp, shouldNotify);
            if (!HeroDeveloperBatchScope.TryEnqueue(message))
            {
                MessageBroker.Instance.Publish(__instance, message);
            }

            if (ModInformation.IsClient) return false;

            // Dedicated servers preserve the requested flag in the batch but do not render local UI.
            shouldNotify = false;
            return true;
        }
    }
}
