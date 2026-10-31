using Common;
using HarmonyLib;
using NavalDLC;
using NavalDLC.CampaignBehaviors;
using System;

namespace Coop.Naval.Storms.Patches;

[HarmonyPatch(typeof(StormCampaignBehavior))]
internal class StormCampaignBehaviorPatches
{
    [HarmonyPatch(nameof(StormCampaignBehavior.HourlyTick))]
    [HarmonyPrefix]
    public static bool HourlyTickPrefix()
    {
        if (ModInformation.IsClient) return false;

        StormPatches.IsHourlyTickInProgress = true;
        return true;
    }

    // One snapshot per storm covers the hourly direction and intensity changes and corrects movement drift on clients
    [HarmonyPatch(nameof(StormCampaignBehavior.HourlyTick))]
    [HarmonyFinalizer]
    public static Exception HourlyTickFinalizer(Exception __exception)
    {
        if (ModInformation.IsClient) return __exception;

        // Finalizer so a throwing tick cant leave ForceDeactivate snapshots switched off
        StormPatches.IsHourlyTickInProgress = false;

        if (__exception == null)
        {
            foreach (var storm in NavalDLCManager.Instance.StormManager._spawnedStorms)
            {
                StormPatches.PublishStateChanged(storm);
            }
        }

        return __exception;
    }
}
