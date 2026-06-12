using Common.Logging;
using HarmonyLib;
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

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.TroopUpgradeTracker), MethodType.Getter)]
    [HarmonyPostfix]
    private static void PostfixTroopUpgradeTracker(MapEvent __instance, TroopUpgradeTracker __result)
    {
        if (__result is null)
        {
            Logger.Error("{Property} was not set propertly for MapEvent {MapEventId}", nameof(MapEvent.TroopUpgradeTracker), __instance.StringId);
            __instance.TroopUpgradeTracker = new TroopUpgradeTracker();
            __result = __instance.TroopUpgradeTracker;
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

    // Under fast-forward churn a map event can tick against a party that was destroyed/desynced
    // mid-tick, NRE-ing inside MapEvent.Update and crashing the server (MapEventManager.Tick runs
    // outside the Campaign.RealTick finalizer). Swallow per-event so one bad event can't take down
    // the rest of the manager tick; the event re-runs on the next tick, so a transient incomplete
    // state resolves itself once the party state settles. Logged at Verbose because this can fire
    // every tick for the duration of the churn and the swallow itself is the recovery.
    [HarmonyPatch(typeof(MapEvent), "Update")]
    [HarmonyFinalizer]
    private static Exception Finalizer_Update(Exception __exception, MapEvent __instance)
    {
        if (__exception != null)
        {
            Logger.Verbose(__exception, "Suppressed {Method} for MapEvent {MapEventId}; will retry next tick", $"{nameof(MapEvent)}.Update", __instance?.StringId);
        }

        return null;
    }

    // A map event side in a save snapshotted mid-churn can reference a leader party whose
    // hero chain no longer resolves; vanilla derefs it unguarded and the throw unwinds through
    // MapEventManager.OnAfterLoad, killing a joining client at the end of the loading screen.
    // Fall back to vanilla's own no-leader default so the load continues.
    [HarmonyPatch(typeof(MapEventSide), nameof(MapEventSide.CacheLeaderSimulationModifier))]
    [HarmonyFinalizer]
    private static Exception Finalizer_CacheLeaderSimulationModifier(Exception __exception, MapEventSide __instance)
    {
        if (__exception != null)
        {
            __instance.LeaderSimulationModifier = 0f;
            Logger.Verbose(__exception, "Suppressed {Method}; defaulted the simulation modifier", $"{nameof(MapEventSide)}.{nameof(MapEventSide.CacheLeaderSimulationModifier)}");
        }

        return null;
    }
}
