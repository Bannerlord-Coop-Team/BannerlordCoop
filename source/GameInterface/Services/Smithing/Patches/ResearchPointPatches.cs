using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.Smithing.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace GameInterface.Services.Smithing.Patches
{
    [HarmonyPatch(typeof(CraftingCampaignBehavior))]
    internal class ResearchPointPatches
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CraftingCampaignBehavior>();

        [HarmonyPatch("AddResearchPoints")]
        [HarmonyPostfix]
        public static void AddResearchPointsPostfix(ref CraftingCampaignBehavior __instance, CraftingTemplate craftingTemplate, int researchPoints)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return;

            // Send updated xp to server
            var message = new ResearchPointsUpdated(Hero.MainHero, craftingTemplate, __instance._openNewPartXpDictionary[craftingTemplate]);
            MessageBroker.Instance.Publish(__instance, message);
        }

        [HarmonyPatch("OpenPart")]
        [HarmonyPostfix]
        public static void OpenPart(ref CraftingCampaignBehavior __instance, CraftingPiece selectedPiece, CraftingTemplate craftingTemplate, bool showNotification = true)
        {
            // Call original if we call this function
            if (CallOriginalPolicy.IsOriginalAllowed()) return;

            // Send unlocked component to server
            var message = new CraftingPartOpened(Hero.MainHero, craftingTemplate, selectedPiece);
            MessageBroker.Instance.Publish(__instance, message);
        }
    }
}
