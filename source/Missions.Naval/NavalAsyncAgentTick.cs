using HarmonyLib;
using Missions.Battles;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

/// <inheritdoc cref="IAsyncAgentTick"/>
/// <remarks>
/// Mission.OnTick clears tickCompleted before the behaviour ticks and starts the async agent tick at its end, and the worker
/// sets it again; so outside Mission.OnTick a cleared tickCompleted means that tick is running.
/// </remarks>
public class NavalAsyncAgentTick : IAsyncAgentTick
{
    private static volatile bool inMissionTick;

    /// <summary>[Game thread] Whether Mission.OnTick is running, set by <see cref="NavalMissionTickWindowPatch"/>.</summary>
    internal static bool InMissionTick
    {
        get => inMissionTick;
        set => inMissionTick = value;
    }

    // Reads the current mission's flag; no mission, no async tick.
    public bool IsRunning
    {
        get
        {
            var mission = Mission.Current;
            return mission != null && IsTickRunning(InMissionTick, mission.tickCompleted);
        }
    }

    /// <summary>Whether the async agent tick runs: tickCompleted is cleared outside Mission.OnTick.</summary>
    internal static bool IsTickRunning(bool inMissionTick, bool tickCompleted) => !inMissionTick && !tickCompleted;
}

/// <summary>Marks the Mission.OnTick window, inside which the async agent tick never runs.</summary>
[HarmonyPatch(typeof(Mission), nameof(Mission.OnTick))]
[HarmonyPatchCategory(NavalMissionModule.PatchCategory)]
internal class NavalMissionTickWindowPatch
{
    // Opens the window.
    [HarmonyPrefix]
    private static void Prefix() => NavalAsyncAgentTick.InMissionTick = true;

    // Closes it, whether OnTick returned or threw.
    [HarmonyFinalizer]
    private static void Finalizer() => NavalAsyncAgentTick.InMissionTick = false;
}
