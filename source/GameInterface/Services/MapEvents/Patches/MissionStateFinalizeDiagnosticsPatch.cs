using Common.Logging;
using HarmonyLib;
using Serilog;
using System.Runtime.CompilerServices;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>Records the mission teardown boundary needed to diagnose later scene-loading crashes.</summary>
[HarmonyPatch(typeof(MissionState), nameof(MissionState.OnFinalize))]
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
