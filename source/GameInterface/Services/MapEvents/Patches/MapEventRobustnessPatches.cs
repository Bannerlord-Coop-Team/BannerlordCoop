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

    // See https://github.com/Bannerlord-Coop-Team/BannerlordCoop/issues/1353
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

    // See https://github.com/Bannerlord-Coop-Team/BannerlordCoop/issues/1353
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
