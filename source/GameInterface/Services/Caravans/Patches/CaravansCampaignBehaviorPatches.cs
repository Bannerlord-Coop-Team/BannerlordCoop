using Common;
using HarmonyLib;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;

namespace GameInterface.Services.Caravans.Patches;

[HarmonyPatch(typeof(CaravansCampaignBehavior))]
internal class CaravansCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(CaravansCampaignBehavior.RegisterEvents))]
    [HarmonyPrefix]
    public static bool RegisterEventsPrefix(ref CaravansCampaignBehavior __instance)
    {
        if (ModInformation.IsServer) return true;

        // Needs to run on the client to initialize carvan dialogue options
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(__instance, new Action<CampaignGameStarter>(__instance.OnSessionLaunched));
        return false;
    }
}
