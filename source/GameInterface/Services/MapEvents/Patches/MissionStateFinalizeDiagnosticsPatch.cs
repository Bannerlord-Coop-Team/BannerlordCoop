using Common.Logging;
using HarmonyLib;
using Serilog;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>Records the mission teardown boundary needed to diagnose later scene-loading crashes.</summary>
[HarmonyPatch]
internal class MissionStateFinalizeDiagnosticsPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<MissionStateFinalizeDiagnosticsPatch>();
    private static readonly ConditionalWeakTable<Mission, MissionCorrelation> Correlations = new();

    internal static void RecordCorrelation(Mission mission, long sequence, string mapEventId)
    {
        Correlations.Remove(mission);
        Correlations.Add(mission, new MissionCorrelation(sequence, mapEventId));
    }

    internal static bool TryGetCorrelation(Mission mission, out long sequence, out string mapEventId)
    {
        if (mission != null && Correlations.TryGetValue(mission, out var correlation))
        {
            sequence = correlation.Sequence;
            mapEventId = correlation.MapEventId;
            return true;
        }

        sequence = 0;
        mapEventId = null;
        return false;
    }

    [HarmonyPatch(typeof(MissionState), nameof(MissionState.OnFinalize))]
    [HarmonyPrefix]
    private static void Prefix(MissionState __instance)
    {
        var mission = __instance.CurrentMission;
        bool hasCorrelation = TryGetCorrelation(mission, out var sequence, out var mapEventId);
        Logger.Information(
            "[BattleMissionLifecycle] Mission finalizing: sequence={Sequence} mapEvent={MapEventId} missionName={MissionName} scene={Scene} missionPresent={MissionPresent} missionEnded={MissionEnded}",
            hasCorrelation ? sequence : (long?)null,
            mapEventId,
            __instance.MissionName,
            mission?.SceneName,
            mission != null,
            mission?.MissionEnded);
    }

    [HarmonyPatch(typeof(Mission), nameof(Mission.EndMission))]
    [HarmonyPrefix]
    internal static void EndMissionPrefix(Mission __instance)
    {
        if (__instance.MissionEnded) return;
        if (!TryGetActiveMapEventId(out var mapEventId)) return;

        Logger.Information(
            "[BattleMissionLifecycle] Mission end requested: mapEvent={MapEventId} state={State} result={Result} caller={Caller}",
            mapEventId,
            __instance.CurrentState,
            __instance.MissionResult?.BattleState,
            System.Environment.StackTrace);
    }

    [HarmonyPatch(typeof(Mission), nameof(Mission.MissionResultReady))]
    [HarmonyPrefix]
    internal static void MissionResultReadyPrefix(Mission __instance, MissionResult missionResult)
    {
        if (!TryGetActiveMapEventId(out var mapEventId)) return;

        Logger.Information(
            "[BattleMissionLifecycle] Mission result ready: mapEvent={MapEventId} state={State} resolved={Resolved} result={Result} caller={Caller}",
            mapEventId,
            __instance.CurrentState,
            missionResult?.BattleResolved,
            missionResult?.BattleState,
            System.Environment.StackTrace);
    }

    // Reads the open coop battle window's map-event id; false for a vanilla battle or when no container exists.
    private static bool TryGetActiveMapEventId(out string mapEventId)
    {
        mapEventId = null;
        if (!ContainerProvider.TryResolve<ICoopBattleMissionRegistry>(out var missionRegistry)) return false;

        mapEventId = missionRegistry.ActiveMapEventId;
        return mapEventId != null;
    }

    /// <summary>Identifies the attack-mission start that created a mission.</summary>
    private sealed class MissionCorrelation
    {
        public long Sequence { get; }
        public string MapEventId { get; }

        public MissionCorrelation(long sequence, string mapEventId)
        {
            Sequence = sequence;
            MapEventId = mapEventId;
        }
    }
}
