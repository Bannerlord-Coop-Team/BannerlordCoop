using Common.Logging;
using HarmonyLib;
using Serilog;
using System;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace GameInterface.Services.GameState.Patches;

[HarmonyPatch]
internal class OnTickRobustnessPatches
{
    private static readonly ILogger Logger = LogManager.GetLogger<OnTickRobustnessPatches>();

    [HarmonyPatch(typeof(Game), nameof(Game.OnTick))]
    [HarmonyFinalizer]
    private static Exception Finalizer_OnTick(Exception __exception)
    {
        if (__exception != null)
        {
            Logger.Error(__exception, "Failed to run {Method}", $"Game.OnTick");
        }

        return null;
    }

    // OnTick clears tickCompleted before the behavior ticks and only the agent tick at its end sets it again,
    // so a throw in between would leave the next OnPreTick spinning in WaitTickCompletion forever.
    // The async agent tick is OnTick's last call, so a caught throw means it was never started.
    [HarmonyPatch(typeof(Mission), nameof(Mission.OnTick))]
    [HarmonyFinalizer]
    internal static void Finalizer_MissionOnTick(Mission __instance, Exception __exception)
    {
        if (__exception == null) return;

        __instance.tickCompleted = true;
    }

    private static long agentTickFailures;
    private static long fixedTickFailures;

    // The engine calls the async agent and team tick on a worker thread with no managed caller, so a throw there crashed the
    // client and left tickCompleted false, freezing the game thread in WaitTickCompletion; it is logged and the tick finished.
    [HarmonyPatch(typeof(Mission), nameof(Mission.TickAgentsAndTeamsImp))]
    [HarmonyFinalizer]
    internal static Exception Finalizer_TickAgentsAndTeams(Mission __instance, Exception __exception)
    {
        if (__exception == null) return null;

        __instance.tickCompleted = true;
        long count = Interlocked.Increment(ref agentTickFailures);
        if (ShouldLogFailure(count))
            Logger.Error(__exception, "Mission agent and team tick threw (failure {Count}); finished the tick so the game thread goes on", count);
        return null;
    }

    // The engine calls the fixed tick with no managed caller either, so a behavior's throw there crashed the client.
    [HarmonyPatch(typeof(Mission), nameof(Mission.OnFixedTick))]
    [HarmonyFinalizer]
    internal static Exception Finalizer_MissionOnFixedTick(Exception __exception)
    {
        if (__exception == null) return null;

        long count = Interlocked.Increment(ref fixedTickFailures);
        if (ShouldLogFailure(count)) Logger.Error(__exception, "Mission fixed tick threw (failure {Count}); skipped the rest of it", count);
        return null;
    }

    /// <summary>Whether a repeating tick failure is logged: the first three, then every power of ten.</summary>
    internal static bool ShouldLogFailure(long count)
    {
        if (count <= 3) return true;

        while (count % 10 == 0) count /= 10;
        return count == 1;
    }
}
