using Common;
using Common.Messaging;
using Coop.Naval.Storms.Interfaces;
using Coop.Naval.Storms.Messages;
using GameInterface;
using GameInterface.Policies;
using HarmonyLib;
using NavalDLC.Map;

namespace Coop.Naval.Storms.Patches;

[HarmonyPatch(typeof(Storm))]
internal class StormPatches
{
    // Set while StormCampaignBehavior.HourlyTick runs, it sends one snapshot per storm once it finishes
    internal static bool IsHourlyTickInProgress;

    // Vanilla calls this every frame while storms overlap or one is out of bounds, so only the first call sends
    [HarmonyPatch(nameof(Storm.ForceDeactivate))]
    [HarmonyPrefix]
    private static void ForceDeactivatePrefix(Storm __instance, out bool __state)
    {
        __state = __instance.IsInFinalizingState;
    }

    // Collisions and the debug command deactivate outside the hourly tick, so send those straight away
    [HarmonyPatch(nameof(Storm.ForceDeactivate))]
    [HarmonyPostfix]
    private static void ForceDeactivatePostfix(Storm __instance, bool __state)
    {
        if (IsHourlyTickInProgress || __state) return;

        PublishStateChanged(__instance);
    }

    internal static void PublishStateChanged(Storm storm)
    {
        if (ModInformation.IsClient) return;
        if (CallOriginalPolicy.IsOriginalAllowed()) return;

        // The constructor also deactivates out of bounds storms, the spawn snapshot is sent once the storm joins the spawned list
        if (!ContainerProvider.TryResolve<IStormInterface>(out var stormInterface)) return;
        if (!stormInterface.IsSpawned(storm)) return;

        MessageBroker.Instance.Publish(storm, new StormStateChanged(storm, isNewStorm: false));
    }
}
