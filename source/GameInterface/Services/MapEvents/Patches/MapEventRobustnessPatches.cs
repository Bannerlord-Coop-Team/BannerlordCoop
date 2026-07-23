using Common.Logging;
using HarmonyLib;
using GameInterface.Services.MapEvents.Initialization;
using SandBox.ViewModelCollection.Map;
using Serilog;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch]
internal class MapEventRobustnessPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapEvent>();
    [ThreadStatic] private static bool restoringTroopUpgradeTracker;

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.TroopUpgradeTracker), MethodType.Getter)]
    [HarmonyPostfix]
    private static void PostfixTroopUpgradeTracker(MapEvent __instance, ref TroopUpgradeTracker __result)
    {
        if (__result is null)
        {
            // The assignment below runs the generated AutoSync setter prefix, which reads this getter
            // to compare values. Let that nested read observe null instead of recursively restoring again.
            if (restoringTroopUpgradeTracker) return;

            // Pending graphs are incomplete by design. A fallback here would replace the registered
            // tracker before its queued reference apply reaches the game thread.
            if (ContainerProvider.TryResolve<IMapEventInitializationBarrier>(out var barrier) &&
                barrier.IsPending(__instance))
            {
                return;
            }

            Logger.Error("{Property} was not properly set for MapEvent {MapEventId}", nameof(MapEvent.TroopUpgradeTracker), __instance.StringId);
            restoringTroopUpgradeTracker = true;
            try
            {
                __result = new TroopUpgradeTracker();
                __instance.TroopUpgradeTracker = __result;
            }
            finally
            {
                restoringTroopUpgradeTracker = false;
            }
        }
    }

    [HarmonyPatch(typeof(MapEventVisualsVM), nameof(MapEventVisualsVM.UpdateMapEventsAux))]
    [HarmonyFinalizer]
    private static Exception Finalizer_UpdateMapEventsAux(Exception __exception, MethodBase __originalMethod)
    {
        if (__exception != null)
        {
            Logger.Error(__exception, "Failed to run {Method}", $"{__originalMethod.DeclaringType}.{__originalMethod.Name}");
        }

        return null;
    }
}
