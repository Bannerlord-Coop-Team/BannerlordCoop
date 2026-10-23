using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <summary>
/// In a co-op naval battle the engine's async agent and team tick runs on a worker with no managed caller, so a throw there
/// crashed the client and left tickCompleted false, freezing the game thread in WaitTickCompletion (R46, R48 L6).
/// </summary>
[HarmonyPatch(typeof(Mission), nameof(Mission.TickAgentsAndTeamsImp))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class NavalAgentTickFailurePatch
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalAgentTickFailurePatch>();

    private static readonly NavalTickFailureCount Failures = new NavalTickFailureCount();

    // Logs the throw, finishes the tick and swallows it in a co-op naval battle; elsewhere vanilla's crash stands.
    [HarmonyFinalizer]
    internal static Exception Finalizer(Mission __instance, Exception __exception)
    {
        if (__exception == null || !CoopNavalMissionScope.IsActive) return __exception;

        __instance.tickCompleted = true;
        long count = Failures.Count(__instance);
        if (NavalTickFailureCount.ShouldLog(count))
            Logger.Error(__exception, "[NavalSync] Mission agent and team tick threw (failure {Count} this mission); finished the tick", count);
        return null;
    }
}

/// <summary>Counts a repeating tick failure per mission, and decides which failures are logged.</summary>
internal class NavalTickFailureCount
{
    private readonly object gate = new object();
    private object mission;
    private long count;

    /// <summary>Counts one failure in <paramref name="currentMission"/>, starting again at 1 in a new mission.</summary>
    public long Count(object currentMission)
    {
        lock (gate)
        {
            if (!ReferenceEquals(mission, currentMission))
            {
                mission = currentMission;
                count = 0;
            }

            return ++count;
        }
    }

    /// <summary>Whether a repeating tick failure is logged: the first three, then every power of ten.</summary>
    public static bool ShouldLog(long count)
    {
        if (count <= 3) return true;

        while (count % 10 == 0) count /= 10;
        return count == 1;
    }
}
