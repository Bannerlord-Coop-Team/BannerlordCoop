using Common;
using Common.Messaging;
using Coop.Naval.Storms.Messages;
using GameInterface.Policies;
using GameInterface.Registry.Auto;
using HarmonyLib;
using NavalDLC.Map;
using System.Collections.Generic;
using TaleWorlds.Library;

namespace Coop.Naval.Storms.Patches;

[HarmonyPatch(typeof(StormManager))]
internal class StormManagerPatches
{
    [HarmonyPatch(nameof(StormManager.CreateStormAtPosition), new[] { typeof(Vec2) })]
    [HarmonyPostfix]
    public static void CreateStormAtPositionPostfix(StormManager __instance) => PublishSpawnedStorm(__instance);

    [HarmonyPatch(nameof(StormManager.CreateStormAtPosition), new[] { typeof(Vec2), typeof(Storm.StormTypes) })]
    [HarmonyPostfix]
    public static void CreateStormAtPositionWithTypePostfix(StormManager __instance) => PublishSpawnedStorm(__instance);

    [HarmonyPatch(nameof(StormManager.CampaignTick))]
    [HarmonyPrefix]
    public static bool CampaignTickPrefix(StormManager __instance, float campaignDt, out List<Storm> __state)
    {
        __state = null;

        if (ModInformation.IsClient)
        {
            // Removal and collision deactivation come from the server, clients only move the storms
            if (campaignDt > 0f)
            {
                foreach (var storm in __instance._spawnedStorms)
                {
                    storm.Tick(campaignDt);
                }
            }

            return false;
        }

        if (campaignDt <= 0f) return true;

        foreach (var storm in __instance._spawnedStorms)
        {
            if (!storm.IsReadyToBeFinalized) continue;

            __state ??= new List<Storm>();
            __state.Add(storm);
        }

        return true;
    }

    [HarmonyPatch(nameof(StormManager.CampaignTick))]
    [HarmonyPostfix]
    public static void CampaignTickPostfix(StormManager __instance, List<Storm> __state)
    {
        if (__state == null) return;

        foreach (var storm in __state)
        {
            if (__instance._spawnedStorms.Contains(storm)) continue;

            MessageBroker.Instance.Publish(storm, new InstanceDestroyed<Storm>(storm));
        }
    }

    public static void PublishSpawnedStorm(StormManager stormManager)
    {
        if (ModInformation.IsClient) return;
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        // Vanilla appends the new storm before firing OnStormCreated
        var storm = stormManager._spawnedStorms[stormManager._spawnedStorms.Count - 1];

        MessageBroker.Instance.Publish(storm, new StormStateChanged(storm, isNewStorm: true));
    }
}
