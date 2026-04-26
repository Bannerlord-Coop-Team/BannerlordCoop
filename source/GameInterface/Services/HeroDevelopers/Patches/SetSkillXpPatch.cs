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
    internal class SetSkillXpPatch
    {
        private static readonly ILogger Logger = LogManager.GetLogger<HeroDeveloper>();

        [HarmonyPatch("SetSkillXp")]
        [HarmonyPrefix]
        public static bool SetSkillXp(ref HeroDeveloper __instance, PropertyObject skill, float value)
        {
            SkillObject skillObject = (SkillObject) skill;

            // Publish message with data
            var message = new SkillXpSet(__instance, skillObject, value);
            MessageBroker.Instance.Publish(__instance, message);

            // Skip original to override original client saving
            return false;
        }
    }
}
