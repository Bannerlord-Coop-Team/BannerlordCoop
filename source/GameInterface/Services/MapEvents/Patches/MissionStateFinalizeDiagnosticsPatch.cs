using Common.Logging;
using HarmonyLib;
using Serilog;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.MapEvents.Patches;

/// <summary>Records the mission teardown boundary needed to diagnose later scene-loading crashes.</summary>
[HarmonyPatch(typeof(MissionState), nameof(MissionState.OnFinalize))]
internal class MissionStateFinalizeDiagnosticsPatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<MissionStateFinalizeDiagnosticsPatch>();

    [HarmonyPrefix]
    private static void Prefix(MissionState __instance)
    {
        var mission = __instance.CurrentMission;
        Logger.Information(
            "[BattleMissionLifecycle] Mission finalizing: missionName={MissionName} scene={Scene} missionPresent={MissionPresent} missionEnded={MissionEnded}",
            __instance.MissionName,
            mission?.SceneName,
            mission != null,
            mission?.MissionEnded);
    }
}
