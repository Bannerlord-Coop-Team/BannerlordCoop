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
            if (restoringTroopUpgradeTracker) return;

            if (ContainerProvider.TryResolve<IMapEventInitializationBarrier>(out var barrier) &&
                barrier.IsPending(__instance))
            {
                return;
            }

            Logger.Error("{Property} was not properly set for MapEvent {MapEventId}", nameof(MapEvent.TroopUpgradeTracker), __instance.StringId);
            restoringTroopUpgradeTracker = true;
            try
            {
                var restoredTracker = new TroopUpgradeTracker();
                foreach (var party in __instance.AttackerSide.Parties)
                {
                    restoredTracker.AddParty(party);
                }
                foreach (var party in __instance.DefenderSide.Parties)
                {
                    restoredTracker.AddParty(party);
                }

                __result = restoredTracker;
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
