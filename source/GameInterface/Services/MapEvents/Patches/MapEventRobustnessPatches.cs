using Common.Logging;
using HarmonyLib;
using SandBox.ViewModelCollection.Map;
using Serilog;
using System;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using GameInterface.Policies;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch]
internal class MapEventRobustnessPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<MapEvent>();

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.TroopUpgradeTracker), MethodType.Getter)]
    [HarmonyPostfix]
    private static void PostfixTroopUpgradeTracker(MapEvent __instance, TroopUpgradeTracker __result)
    {
        if (CallOriginalPolicy.IsOriginalAllowed()) return;
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
}
