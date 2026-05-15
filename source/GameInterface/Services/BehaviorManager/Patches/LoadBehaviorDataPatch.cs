using Common;
using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.BehaviorManager.Patches
{
    [HarmonyPatch(typeof(CampaignBehaviorManager))]
    internal class LoadBehaviorDataPatch
    {
        private static readonly ILogger Logger = LogManager.GetLogger<CampaignBehaviorManager>();

        [HarmonyPatch("LoadBehaviorData")]
        [HarmonyPrefix]
        public static bool LoadBehaviorData(ref CampaignBehaviorManager __instance)
        {
            if (ModInformation.IsClient) return true;

            foreach (CampaignBehaviorBase campaignBehavior in __instance._campaignBehaviors)
            {
                __instance._campaignBehaviorDataStore.LoadBehaviorData(campaignBehavior);
            }
            //_instance._campaignBehaviorDataStore.ClearBehaviorData();

            // Skip original on server
            return false;
        }
    }
}
