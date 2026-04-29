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
            // Publish message with data
            var message = new SkillLevelChange(__instance, skill, changeAmount, shouldNotify);
            MessageBroker.Instance.Publish(__instance, message);

            // Skip original to override original client saving
            return false;
        }
    }
}
