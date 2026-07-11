using Common;
using Common.Logging;
using Common.Util;
using GameInterface.Policies;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Initialization;
using HarmonyLib;
using Serilog;
using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>
/// Defines the lifetime of a MapEvent initialization transaction. Constructor scopes let the
/// registry associate nested objects with their root, Initialize opens the build window, and the
/// manager registration commits the finished graph as one aggregate message.
/// </summary>
[HarmonyPatch]
internal static class MapEventInitializationPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(MapEventInitializationPatches));

    [HarmonyPatch(typeof(MapEvent), MethodType.Constructor)]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void Prefix_MapEventConstructor(ref IDisposable __state)
    {
        if (!ShouldTrackServerConstruction()) return;

        if (ContainerProvider.TryResolve<IMapEventInitializationTracker>(out var tracker))
        {
            __state = tracker.BeginMapEventConstruction();
        }
    }

    [HarmonyPatch(typeof(MapEvent), MethodType.Constructor)]
    [HarmonyFinalizer]
    private static Exception Finalizer_MapEventConstructor(
        MapEvent __instance,
        Exception __exception,
        IDisposable __state)
    {
        __state?.Dispose();

        if (__state != null && __exception != null && __instance != null &&
            ContainerProvider.TryResolve<IMapEventInitializationTracker>(out var tracker))
        {
            tracker.AbortBuild(__instance);
        }

        return __exception;
    }

    [HarmonyPatch(typeof(MapEventParty), MethodType.Constructor, typeof(PartyBase))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void Prefix_MapEventPartyConstructor(PartyBase party, ref IDisposable __state)
    {
        if (!ShouldTrackServerConstruction()) return;

        if (ContainerProvider.TryResolve<IMapEventInitializationTracker>(out var tracker))
        {
            __state = tracker.BeginMapEventPartyConstruction(party);
        }
    }

    [HarmonyPatch(typeof(MapEventParty), MethodType.Constructor, typeof(PartyBase))]
    [HarmonyFinalizer]
    private static Exception Finalizer_MapEventPartyConstructor(Exception __exception, IDisposable __state)
    {
        __state?.Dispose();
        return __exception;
    }

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.Initialize))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void Prefix_MapEventInitialize(MapEvent __instance, ref bool __state)
    {
        if (!ShouldTrackServerConstruction()) return;

        if (!ContainerProvider.TryResolve<IMapEventInitializationTracker>(out var tracker))
        {
            throw new InvalidOperationException(
                $"Unable to start aggregate initialization for {nameof(MapEvent)}: tracker is unavailable");
        }

        tracker.BeginBuild(__instance);
        __state = true;
    }

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.Initialize))]
    [HarmonyFinalizer]
    private static Exception Finalizer_MapEventInitialize(
        MapEvent __instance,
        Exception __exception,
        bool __state)
    {
        if (!__state) return __exception;

        if (ContainerProvider.TryResolve<IMapEventInitializationTracker>(out var tracker))
        {
            tracker.EndInitialization(__instance);

            if (__exception == null)
                tracker.CompletePublishedBuild(__instance);
            else
                tracker.AbortBuild(__instance);
        }

        if (__exception != null)
            Logger.Error(__exception, "Map event initialization failed; rolled back its aggregate build");

        return __exception;
    }

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.AddInvolvedPartyInternal))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void Prefix_MapEventAddInvolvedPartyInternal(
        MapEvent __instance,
        ref IDisposable __state)
    {
        if (!ShouldTrackServerConstruction()) return;

        if (ContainerProvider.TryResolve<IMapEventInitializationTracker>(out var tracker))
        {
            __state = tracker.BeginGraphChildConstruction(__instance);
        }
    }

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.AddInvolvedPartyInternal))]
    [HarmonyFinalizer]
    private static Exception Finalizer_MapEventAddInvolvedPartyInternal(
        Exception __exception,
        IDisposable __state)
    {
        __state?.Dispose();
        return __exception;
    }

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.FinalizeEventAux))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void Prefix_MapEventFinalize(MapEvent __instance)
    {
        // An event can exceptionally finish in the frame in which it was created. Publish its complete
        // graph before the ordinary destroy packets so clients never receive a destroy for an unseen root.
        CommitIfReady(__instance, terminalInitialization: true);
    }

    // OnMapEventCreated is a tiny method whose native x64 unwind metadata can be corrupted by a Harmony
    // entry detour. Commit from the manager's first Tick instead; by then registration and every factory's
    // post-Initialize additions are complete, but no MapEvent.Update has run yet.
    [HarmonyPatch(typeof(MapEventManager), nameof(MapEventManager.Tick))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void Prefix_MapEventManagerTick(MapEventManager __instance)
    {
        if (ModInformation.IsClient || CallOriginalPolicy.IsOriginalAllowed()) return;

        if (!ContainerProvider.TryResolve<IMapEventInitializationTracker>(out var tracker)) return;

        List<MapEvent> pending = null;
        foreach (var mapEvent in __instance.MapEvents)
        {
            if (!tracker.IsBuilding(mapEvent)) continue;

            pending ??= new List<MapEvent>();
            pending.Add(mapEvent);
        }

        if (pending == null) return;

        if (!ContainerProvider.TryResolve<MapEventInitializationHandler>(out var handler))
        {
            throw new InvalidOperationException(
                $"Unable to commit aggregate initialization for {nameof(MapEvent)}: handler is unavailable");
        }

        foreach (var mapEvent in pending)
            handler.Publish(mapEvent);
    }

    private static void CommitIfReady(MapEvent mapEvent, bool terminalInitialization = false)
    {
        if (ModInformation.IsClient || CallOriginalPolicy.IsOriginalAllowed() || mapEvent == null) return;

        if (!ContainerProvider.TryResolve<IMapEventInitializationTracker>(out var tracker) ||
            !tracker.IsBuilding(mapEvent))
        {
            return;
        }

        if (!ContainerProvider.TryResolve<MapEventInitializationHandler>(out var handler))
        {
            tracker.AbortBuild(mapEvent);
            throw new InvalidOperationException(
                $"Unable to commit aggregate initialization for {nameof(MapEvent)}: handler is unavailable");
        }

        handler.Publish(mapEvent, terminalInitialization);
    }

    private static bool ShouldTrackServerConstruction() =>
        ModInformation.IsServer && !CallOriginalPolicy.IsOriginalAllowed();
}
