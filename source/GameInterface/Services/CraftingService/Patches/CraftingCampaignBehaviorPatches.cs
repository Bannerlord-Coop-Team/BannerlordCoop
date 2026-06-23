using Common;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.CraftingService.Patches
{
    [HarmonyPatch(typeof(CraftingCampaignBehavior))]
    internal class CraftingCampaignBehaviorPatches
    {
        [HarmonyPatch(nameof(CraftingCampaignBehavior.RegisterEvents))]
        [HarmonyPrefix]
        public static bool RegisterEventsPrefix(ref CraftingCampaignBehavior __instance)
        {
            if (ModInformation.IsServer) return true;

            // Needed to load smithy dialogue
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(__instance, new Action<CampaignGameStarter>(__instance.OnSessionLaunched));
            return false;
        }
    }
}