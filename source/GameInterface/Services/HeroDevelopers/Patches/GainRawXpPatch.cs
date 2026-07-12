using Common;
using Common.Logging;
using Common.Messaging;
using GameInterface.Policies;
using GameInterface.Services.HeroDevelopers.Messages;
using HarmonyLib;
using Serilog;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;

namespace GameInterface.Services.HeroDevelopers.Patches
{
    [HarmonyPatch(typeof(HeroDeveloper))]
    internal class GainRawXpPatch
    {
        private static readonly ILogger Logger = LogManager.GetLogger<HeroDeveloper>();

        [HarmonyPatch(nameof(HeroDeveloper.GainRawXp))]
        [HarmonyPrefix]
        public static bool GainRawXpPrefix(ref HeroDeveloper __instance, float rawXp, bool shouldNotify)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;

            // Don't allow clients to change raw xp
            if (ModInformation.IsClient) return false;

            return true;
        }
    }
}
