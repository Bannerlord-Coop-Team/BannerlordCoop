#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using NavalDLC.Missions.Objects.UsableMachines;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

internal static partial class NavalLabPresentationPatches
{
    // Retains only values already consumed by this native callback, with unread inputs left null.
    private sealed class StopInputs
    {
        internal bool? Sitting, Struck;
        internal AgentMovementLockedState? MovementLocked;
        internal readonly List<float?> RemovalTimes = new List<float?>();
        internal readonly List<float?> MissionTimes = new List<float?>();
        internal readonly List<int> CurrentActions = new List<int>();
        internal readonly List<bool> Alternatives = new List<bool>();

        // Copies the observations before native stop cleanup can change the callback state.
        internal object Snapshot() => new
        {
            sitting = Sitting, struck = Struck, movementLocked = MovementLocked?.ToString(),
            removalTimes = RemovalTimes.ToArray(), missionTimes = MissionTimes.ToArray(),
            currentActions = CurrentActions.ToArray(), alternatives = Alternatives.ToArray()
        };
    }

    // Observes existing stack values and identifies each stop call without changing its arguments.
    internal static IEnumerable<CodeInstruction> InstrumentOarStopInputs(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();
        var observers = new Dictionary<MethodInfo, (string Name, int Count)>
        {
            [AccessTools.PropertyGetter(typeof(Agent), nameof(Agent.MovementLockedState))] = (nameof(ObserveStopMovement), 1),
            [AccessTools.PropertyGetter(typeof(Agent), nameof(Agent.IsInBeingStruckAction))] = (nameof(ObserveStopStruck), 2),
            [AccessTools.Method(typeof(Agent), nameof(Agent.GetCurrentAction))] = (nameof(ObserveStopAction), 4),
            [AccessTools.Method(typeof(MBActionSet), nameof(MBActionSet.AreActionsAlternatives))] = (nameof(ObserveStopAlternative), 2),
            [AccessTools.PropertyGetter(typeof(Mission), nameof(Mission.CurrentTime))] = (nameof(ObserveStopMissionTime), 2)
        };
        var sitting = AccessTools.Field(typeof(ShipOarMachine), "_isPilotSitting");
        var removalTime = AccessTools.Field(typeof(ValueTuple<float, Agent.StopUsingGameObjectFlags>), "Item1");
        var stop = AccessTools.Method(typeof(Agent), nameof(Agent.StopUsingGameObjectMT));
        if (observers.Any(pair => code.Count(instruction => instruction.Calls(pair.Key)) != pair.Value.Count)
            || code.Count(instruction => instruction.LoadsField(sitting)) != 2
            || code.Count(instruction => instruction.LoadsField(removalTime)) != 2
            || code.Count(instruction => instruction.Calls(stop)) != 3)
            throw new InvalidOperationException("Unsupported native oar stop callsites.");

        int stopOrdinal = 0;
        foreach (var instruction in code)
        {
            if (instruction.Calls(stop))
            {
                // Keep branch labels on the first replacement instruction so the ordinal is always pushed.
                yield return new CodeInstruction(instruction) { opcode = OpCodes.Ldc_I4, operand = stopOrdinal++ };
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(NavalLabPresentationPatches), nameof(TraceStationStop)));
                continue;
            }
            yield return instruction;
            string observer = instruction.LoadsField(sitting) ? nameof(ObserveStopSitting)
                : instruction.LoadsField(removalTime) ? nameof(ObserveStopRemovalTime) : null;
            if (instruction.operand is MethodInfo method && instruction.Calls(method) && observers.TryGetValue(method, out var observation))
                observer = observation.Name;
            if (observer != null)
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(NavalLabPresentationPatches), observer));
        }
    }

    // Records the sitting predicate without rereading the machine.
    private static bool ObserveStopSitting(bool value)
    { if (oarScope?.StopInputs is { } inputs) inputs.Sitting = value; return value; }

    // Records the struck predicate without another native query.
    private static bool ObserveStopStruck(bool value)
    { if (oarScope?.StopInputs is { } inputs) inputs.Struck = value; return value; }

    // Records the movement predicate without another native query.
    private static AgentMovementLockedState ObserveStopMovement(AgentMovementLockedState value)
    { if (oarScope?.StopInputs is { } inputs) inputs.MovementLocked = value; return value; }

    // Records action reads in execution order, including repeated channel-zero reads during seating.
    private static ActionIndexCache ObserveStopAction(ActionIndexCache value)
    { oarScope?.StopInputs?.CurrentActions.Add(value.Index); return value; }

    // Records each evaluated alternatives predicate; short-circuited checks remain absent.
    private static bool ObserveStopAlternative(bool value)
    { oarScope?.StopInputs?.Alternatives.Add(value); return value; }

    // Records each timer read while retaining non-finite values on the native stack.
    private static float ObserveStopRemovalTime(float value)
    { oarScope?.StopInputs?.RemovalTimes.Add(Finite(value)); return value; }

    // Records the actual clock read rather than querying it again at stop time.
    private static float ObserveStopMissionTime(float value)
    { oarScope?.StopInputs?.MissionTimes.Add(Finite(value)); return value; }

    // Exposes this callsite only for its exact pilot while the original stop call is executing.
    internal static object StationStopCallsite(Agent agent) => agent != null && oarScope?.StopInputs != null
        && ReferenceEquals(oarScope.Machine.PilotAgent, agent) ? oarScope.StopCallsite : null;

    // Runs the original stop once with unchanged arguments and restores the diagnostic scope even on failure.
    private static void TraceStationStop(Agent agent, bool successful, Agent.StopUsingGameObjectFlags flags, int ordinal)
    {
        var scope = oarScope;
        object previous = scope?.StopCallsite;
        if (scope?.StopInputs != null && ReferenceEquals(scope.Machine.PilotAgent, agent))
            scope.StopCallsite = new
            {
                ordinal, branch = ordinal == 0 ? "delayed_pilot_removal" : ordinal == 1 ? "seating_action_mismatch" : "rowing_action_mismatch",
                inputs = scope.StopInputs.Snapshot()
            };
        try { agent.StopUsingGameObjectMT(successful, flags); }
        finally { if (scope != null) scope.StopCallsite = previous; }
    }
}
#endif
