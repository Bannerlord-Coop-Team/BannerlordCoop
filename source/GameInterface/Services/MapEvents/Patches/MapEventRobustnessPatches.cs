using Common;
using Common.Logging;
using Common.Util;
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
            // A client must wait for the authoritative registered tracker rather than creating an
            // unregistered local replacement while a replicated reference is still in flight.
            if (ModInformation.IsClient) return;

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
                PopulateTrackerFromMapEvent(__instance, __result);
                __instance.TroopUpgradeTracker = __result;
            }
            finally
            {
                restoringTroopUpgradeTracker = false;
            }
        }
    }

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.TroopUpgradeTracker), MethodType.Setter)]
    [HarmonyPostfix]
    private static void PostfixTroopUpgradeTrackerSet(MapEvent __instance, TroopUpgradeTracker value)
    {
        // The initial graph commit repopulates the tracker in FinishClient. Only post-commit,
        // network-applied replacement references need this local mirror of the server recovery.
        if (!ModInformation.IsClient || !AllowedThread.IsThisThreadAllowed() || value is null ||
            value._mapEventParties.Count != 0)
        {
            return;
        }

        if (ContainerProvider.TryResolve<IMapEventInitializationBarrier>(out var barrier) &&
            barrier.IsPending(__instance))
        {
            return;
        }

        PopulateTrackerFromMapEvent(__instance, value);
    }

    private static void PopulateTrackerFromMapEvent(MapEvent mapEvent, TroopUpgradeTracker tracker)
    {
        if (mapEvent._sides is null) return;

        foreach (var side in mapEvent._sides)
        {
            if (side?.Parties is null) continue;
            foreach (var party in side.Parties)
            {
                if (party is not null) tracker.AddParty(party);
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
