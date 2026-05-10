using Common;
using Common.Logging;
using Common.Messaging;
using Common.Util;
using GameInterface.Services.Smithing.Messages;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Settlements;

namespace GameInterface.Services.Smithing.Patches
{
    [HarmonyPatch(typeof(CraftingCampaignBehavior))]
    internal class TickPatches
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CraftingCampaignBehavior>();

        [HarmonyPatch("HourlyTick")]
        [HarmonyPrefix]
        public static bool HourlyTick(ref CraftingCampaignBehavior __instance)
        {
            // Only let server handle ticks
            if (ModInformation.IsClient) return false;

            // Update on all clients with message
            var message = new HourTicked(__instance);
            MessageBroker.Instance.Publish(__instance, message);

            // Run on server
            return true;
        }

        [HarmonyPatch("DailyTickSettlement")]
        [HarmonyPrefix]
        public static bool DailyTickSettlement(ref CraftingCampaignBehavior __instance, Settlement settlement)
        {
            // Only let server handle ticks
            if (ModInformation.IsClient) return false;

            // Run on server
            return true;
        }

        [HarmonyPatch("DailyTick")]
        [HarmonyPrefix]
        public static bool DailyTick(ref CraftingCampaignBehavior __instance)
        {
            // Only let server handle ticks
            if (ModInformation.IsClient) return false;

            // Run on server
            return true;
        }
    }
}
