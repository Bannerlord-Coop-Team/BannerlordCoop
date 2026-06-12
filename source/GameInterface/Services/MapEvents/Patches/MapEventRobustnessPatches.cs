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

    // Fast-forwarding the campaign resolves many battles in one burst, so a party can be
    // destroyed (its battle finished, the party wiped out or disbanded) while a later map event
    // in the same tick still references it. MapEvent.Update then dereferences the missing party
    // and crashes the server, because MapEventManager.Tick runs outside the Campaign.RealTick
    // finalizer. Swallow per-event so one bad event can't take down the rest of the manager
    // tick; the event re-runs on the next tick and proceeds normally once its parties are in a
    // consistent state again. Logged at Verbose because it can fire every tick until then and
    // the swallow itself is the recovery.
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

    // The save sent to a joining client is a snapshot of the live world, and while the campaign
    // is fast-forwarded that snapshot can be taken in the middle of such a burst: a battle side
    // can be captured with its leader party's hero already removed. Vanilla dereferences that
    // leader unguarded while the client loads the save, and the throw unwinds through
    // MapEventManager.OnAfterLoad, killing the client at the end of the loading screen. Fall
    // back to vanilla's own no-leader default so the load continues.
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
