using Common;
using GameInterface.Policies;
using GameInterface.Services.MapEvents.Initialization;
using HarmonyLib;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Brackets server MapEvent construction with one initialization barrier. The first manager tick
/// publishes completed graphs before vanilla updates them, while exceptional and early-finalization
/// paths publish a terminal graph before their destroy stream begins.
/// </summary>
[HarmonyPatch]
internal static class MapEventInitializationPatches
{
    [HarmonyPatch(typeof(MapEvent), MethodType.Constructor)]
    [HarmonyFinalizer]
    [HarmonyPriority(Priority.First)]
    private static Exception Finalizer_MapEventConstructor(MapEvent __instance, Exception __exception)
    {
        if (__exception != null && TryResolveServerBarrier(out var barrier))
            barrier.AbortServer(__instance);

        return __exception;
    }

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.Initialize))]
    [HarmonyFinalizer]
    [HarmonyPriority(Priority.First)]
    private static Exception Finalizer_MapEventInitialize(
        MapEvent __instance,
        Exception __exception)
    {
        if (__exception != null && TryResolveServerBarrier(out var barrier))
            barrier.AbortServer(__instance);

        return __exception;
    }

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.AddInvolvedPartyInternal))]
    [HarmonyPostfix]
    private static void Postfix_MapEventAddInvolvedParty(MapEvent __instance, MapEventParty mapEventParty)
    {
        if (TryResolveServerBarrier(out var barrier))
            barrier.TrackParty(__instance, mapEventParty);
    }

    [HarmonyPatch(typeof(MapEventManager), nameof(MapEventManager.Tick))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void Prefix_MapEventManagerTick(MapEventManager __instance)
    {
        if (!TryResolveServerBarrier(out var barrier)) return;

        var mapEvents = new List<MapEvent>(__instance.MapEvents);
        foreach (var mapEvent in mapEvents)
            barrier.CommitServer(mapEvent);
    }

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.FinalizeEventAux))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void Prefix_MapEventFinalizeEventAux(MapEvent __instance)
    {
        if (TryResolveServerBarrier(out var barrier))
            barrier.CommitTerminalServer(__instance);
    }

    private static bool TryResolveServerBarrier(out IMapEventInitializationBarrier barrier)
    {
        barrier = null;
        if (!ModInformation.IsServer ||
            !ContainerProvider.TryResolve(out barrier) ||
            CallOriginalPolicy.IsOriginalAllowed())
        {
            barrier = null;
            return false;
        }

        return true;
    }
}
