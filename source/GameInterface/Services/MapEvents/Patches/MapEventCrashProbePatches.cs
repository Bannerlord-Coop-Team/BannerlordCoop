using Common.Logging;
using GameInterface.Services.MapEvents.Diagnostics;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;

namespace GameInterface.Services.MapEvents.Patches;

[HarmonyPatch]
internal static class MapEventCrashProbePatches
{
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(MapEventCrashProbePatches));

    [HarmonyPatch(typeof(MapEventManager), nameof(MapEventManager.Tick))]
    [HarmonyPrefix]
    private static void PrefixMapEventManagerTick()
    {
        MapEventCrashProbe.Record("MapEventManager.Tick:enter");
    }

    [HarmonyPatch(typeof(MapEventManager), nameof(MapEventManager.Tick))]
    [HarmonyFinalizer]
    private static Exception FinalizerMapEventManagerTick(Exception __exception)
    {
        RecordCompletion("MapEventManager.Tick", __exception);
        return __exception;
    }

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.Update))]
    [HarmonyPrefix]
    private static void PrefixMapEventUpdate(MapEvent __instance)
    {
        MapEventCrashProbe.RecordMapEvent("MapEvent.Update:enter", __instance);
    }

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.Update))]
    [HarmonyFinalizer]
    private static Exception FinalizerMapEventUpdate(MapEvent __instance, Exception __exception)
    {
        if (__exception == null)
        {
            MapEventCrashProbe.RecordMapEvent("MapEvent.Update:completed", __instance);
        }
        else
        {
            MapEventCrashProbe.RecordMapEvent("MapEvent.Update:exception", __instance);
            Logger.Error(
                __exception,
                "[MapEventCrashProbe] managed exception in MapEvent.Update for {MapEventId}",
                __instance?.StringId ?? "null");
        }

        return __exception;
    }

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.CheckRunAway))]
    [HarmonyPrefix]
    private static void PrefixCheckRunAway(MapEvent __instance)
    {
        MapEventCrashProbe.RecordMapEvent("MapEvent.CheckRunAway:enter", __instance);
    }

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.CheckRunAway))]
    [HarmonyFinalizer]
    private static Exception FinalizerCheckRunAway(MapEvent __instance, Exception __exception)
    {
        return RecordMapEventCompletion("MapEvent.CheckRunAway", __instance, __exception);
    }

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.SimulateBattleSessionForMapEvent))]
    [HarmonyPrefix]
    private static void PrefixSimulateBattleSession(MapEvent __instance)
    {
        MapEventCrashProbe.RecordMapEvent("MapEvent.SimulateBattleSessionForMapEvent:enter", __instance);
    }

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.SimulateBattleSessionForMapEvent))]
    [HarmonyFinalizer]
    private static Exception FinalizerSimulateBattleSession(MapEvent __instance, Exception __exception)
    {
        return RecordMapEventCompletion("MapEvent.SimulateBattleSessionForMapEvent", __instance, __exception);
    }

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.FinishBattle))]
    [HarmonyPrefix]
    private static void PrefixFinishBattle(MapEvent __instance)
    {
        MapEventCrashProbe.RecordMapEvent("MapEvent.FinishBattle:enter", __instance);
    }

    [HarmonyPatch(typeof(MapEvent), nameof(MapEvent.FinishBattle))]
    [HarmonyFinalizer]
    private static Exception FinalizerFinishBattle(MapEvent __instance, Exception __exception)
    {
        return RecordMapEventCompletion("MapEvent.FinishBattle", __instance, __exception);
    }

    [HarmonyPatch(typeof(CampaignTickCacheDataStore), nameof(CampaignTickCacheDataStore.RealTick))]
    [HarmonyPrefix]
    private static void PrefixCampaignRealTick()
    {
        MapEventCrashProbe.Record("CampaignTickCacheDataStore.RealTick:enter");
    }

    [HarmonyPatch(typeof(CampaignTickCacheDataStore), nameof(CampaignTickCacheDataStore.RealTick))]
    [HarmonyFinalizer]
    private static Exception FinalizerCampaignRealTick(Exception __exception)
    {
        RecordCompletion("CampaignTickCacheDataStore.RealTick", __exception);
        return __exception;
    }

    private static void RecordCompletion(string operation, Exception exception)
    {
        if (exception == null)
        {
            MapEventCrashProbe.Record(operation + ":completed");
            return;
        }

        MapEventCrashProbe.RecordException(operation + ":exception", exception);
        Logger.Error(exception, "[MapEventCrashProbe] managed exception in {Operation}", operation);
    }

    private static Exception RecordMapEventCompletion(string operation, MapEvent mapEvent, Exception exception)
    {
        if (exception == null)
        {
            MapEventCrashProbe.RecordMapEvent(operation + ":completed", mapEvent);
            return null;
        }

        MapEventCrashProbe.RecordMapEvent(operation + ":exception", mapEvent);
        Logger.Error(
            exception,
            "[MapEventCrashProbe] managed exception in {Operation} for {MapEventId}",
            operation,
            mapEvent?.StringId ?? "null");
        return exception;
    }
}
