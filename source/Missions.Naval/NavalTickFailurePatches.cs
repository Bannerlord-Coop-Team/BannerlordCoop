using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using System.Diagnostics;
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
    // Static as the patch is; it keeps the failing mission only weakly.
    private static readonly INavalTickFailureLog Failures = new NavalTickFailureLog();

    // Logs the throw, finishes the tick and swallows it in a co-op naval battle; elsewhere vanilla's crash stands.
    [HarmonyFinalizer]
    internal static Exception Finalizer(Mission __instance, Exception __exception)
    {
        if (__exception == null || !CoopNavalMissionScope.IsActive) return __exception;

        __instance.tickCompleted = true;
        Failures.Report(__exception, "Mission agent and team tick (finished the tick)", __instance);
        return null;
    }
}

/// <summary>Logs a repeating tick failure without flooding the log, counting per mission.</summary>
public interface INavalTickFailureLog
{
    /// <summary>Counts a failure of <paramref name="what"/> in <paramref name="mission"/>, logs it when its turn comes, and returns the count.</summary>
    long Report(Exception exception, string what, object mission);
}

/// <inheritdoc cref="INavalTickFailureLog"/>
public class NavalTickFailureLog : INavalTickFailureLog
{
    private static readonly ILogger Logger = LogManager.GetLogger<NavalTickFailureLog>();

    // A repeating failure still gets a line at most this often, with its running count.
    internal const double SummarySeconds = 10;

    private readonly object gate = new object();
    private readonly WeakReference<object> mission = new WeakReference<object>(null);
    private long count;
    private long lastLineAt;

    // Called from the async worker or a fixed tick, so the count and the last line's time are read and written under a lock.
    public long Report(Exception exception, string what, object currentMission)
    {
        long failures;
        lock (gate)
        {
            if (!mission.TryGetTarget(out var known) || !ReferenceEquals(known, currentMission))
            {
                mission.SetTarget(currentMission);
                count = 0;
                lastLineAt = 0;
            }

            failures = ++count;
            long now = Stopwatch.GetTimestamp();
            double sinceLastLine = lastLineAt == 0 ? double.MaxValue : (double)(now - lastLineAt) / Stopwatch.Frequency;
            if (!ShouldLog(failures, sinceLastLine)) return failures;

            lastLineAt = now;
        }

        Logger.Error(exception, "[NavalSync] {What} threw (failure {Count} this mission)", what, failures);
        return failures;
    }

    /// <summary>
    /// Whether a repeating failure is logged: the first three, every power of ten, and otherwise once <see cref="SummarySeconds"/>
    /// passed since the last line.
    /// </summary>
    internal static bool ShouldLog(long count, double secondsSinceLastLine)
    {
        if (count <= 3 || secondsSinceLastLine >= SummarySeconds) return true;

        while (count % 10 == 0) count /= 10;
        return count == 1;
    }
}
