using Common.Logging;
using Common.Messaging;
using GameInterface.Services.HeroDevelopers.Messages;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Library;
using GameInterface.Policies;

namespace GameInterface.Services.HeroDevelopers.Patches
{
    [HarmonyPatch(typeof(HeroDeveloper))]
    internal class GainRawXpPatch
    {
        private static readonly ILogger Logger = LogManager.GetLogger<HeroDeveloper>();

        [HarmonyPatch("GainRawXp")]
        [HarmonyPrefix]
        public static bool GainRawXp(ref HeroDeveloper __instance, float rawXp, bool shouldNotify)
        {
            if (CallOriginalPolicy.IsOriginalAllowed()) return true;
            // Publish message with data
            var message = new RawXpGain(__instance, rawXp, shouldNotify);
            MessageBroker.Instance.Publish(__instance, message);

            // Skip original to override original client saving
            return false;
        }
    }
}
