#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using NavalDLC.Missions.ShipActuators;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace Missions.Naval;

internal static partial class NavalLabPresentationPatches
{
    internal static IEnumerable<CodeInstruction> InstrumentOarCallsites(IEnumerable<CodeInstruction> instructions)
    {
        var code = instructions.ToList();
        var observers = new Dictionary<MethodInfo, string>
        {
            [AccessTools.PropertyGetter(typeof(MissionOar), nameof(MissionOar.IsExtracted))] = nameof(ConsumeExtracted),
            [AccessTools.PropertyGetter(typeof(MissionOar), nameof(MissionOar.NeededRevolutionRate))] = nameof(ConsumeRate),
            [AccessTools.PropertyGetter(typeof(MissionOar), nameof(MissionOar.VisualPhase))] = nameof(ConsumePhase),
            [AccessTools.Method(typeof(MissionOar), nameof(MissionOar.IsInRowingMotion))] = nameof(ConsumeMotion),
            [AccessTools.Method(typeof(MissionOar), nameof(MissionOar.ComputeOarEntityFrame))] = nameof(ObserveBladeFrame)
        };
        var setAction = AccessTools.Method(typeof(Agent), nameof(Agent.SetActionChannel));
        var setHandIk = AccessTools.Method(typeof(Agent), nameof(Agent.SetHandInverseKinematicsFrame));
        if (observers.Any(pair => code.Count(instruction => instruction.Calls(pair.Key)) != (pair.Value == nameof(ConsumeExtracted) ? 2 : 1))
            || code.Count(instruction => instruction.Calls(setAction)) != 6 || code.Count(instruction => instruction.Calls(setHandIk)) != 1)
            throw new InvalidOperationException("Unsupported native oar presentation callsites.");
        foreach (var instruction in code)
        {
            if (instruction.Calls(setAction) || instruction.Calls(setHandIk))
            {
                var replacement = new CodeInstruction(instruction) { opcode = OpCodes.Call,
                    operand = AccessTools.Method(typeof(NavalLabPresentationPatches), instruction.Calls(setAction) ? nameof(TraceSetAction) : nameof(TraceHandIk)) };
                yield return replacement;
            }
            else
            {
                yield return instruction;
                if (instruction.operand is MethodInfo method && observers.TryGetValue(method, out var observer)
                    && (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt))
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(NavalLabPresentationPatches), observer));
            }
        }
    }

    // Keep the original read once, then supply the scoped presentation value to vanilla's branch.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ConsumeExtracted(bool value)
    {
        bool consumed = oarScope?.PresentationState is { } state ? state.Extraction >= 1 : value;
        if (oarScope?.TraceEnabled == true && ++oarScope.ExtractedReads == 1)
        { oarScope.RawExtracted = value; oarScope.Extracted = consumed; }
        return consumed;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float ConsumeRate(float value)
    {
        float consumed = oarScope?.PresentationState?.NeededRate ?? value;
        if (oarScope?.TraceEnabled == true)
        { oarScope.RateReads++; oarScope.RawRate = Finite(value); oarScope.Rate = Finite(consumed); }
        return consumed;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static float ConsumePhase(float value)
    {
        float consumed = oarScope?.PresentationState?.Phase ?? value;
        if (oarScope?.TraceEnabled == true)
        { oarScope.PhaseReads++; oarScope.RawPhase = Finite(value); oarScope.Phase = Finite(consumed); }
        return consumed;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ConsumeMotion(bool value)
    {
        bool consumed = oarScope?.PresentationState?.Rowing ?? value;
        if (oarScope?.TraceEnabled == true)
        { oarScope.MotionReads++; oarScope.RawRowing = value; oarScope.Rowing = consumed; }
        return consumed;
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static MatrixFrame ObserveBladeFrame(MatrixFrame value)
    { if (oarScope?.TraceEnabled == true) oarScope.BladeFrameCalls++; return value; }
    private static float? Finite(float value) => float.IsNaN(value) || float.IsInfinity(value) ? null : value;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TraceSetAction(Agent agent, int channel, in ActionIndexCache action, bool ignorePriority,
        AnimFlags flags, float blendWithNextActionFactor, float actionSpeed, float blendInPeriod, float blendOutPeriodToNoAnim,
        float startProgress, bool useLinearSmoothing, float blendOutPeriod, int actionShift, bool forceFaceMorphRestart)
    {
        bool trace = oarScope?.TraceEnabled == true && channel >= 0 && channel < 2 && agent == oarScope.Machine.PilotAgent;
        int before = trace ? agent.GetCurrentAction(channel).Index : 0;
        bool result = agent.SetActionChannel(channel, in action, ignorePriority, flags, blendWithNextActionFactor, actionSpeed,
            blendInPeriod, blendOutPeriodToNoAnim, startProgress, useLinearSmoothing, blendOutPeriod, actionShift, forceFaceMorphRestart);
        if (trace)
        {
            var call = oarScope.Actions[channel] ?? (oarScope.Actions[channel] = new ActionCall());
            call.Calls++; call.Requested = action.Index + actionShift; call.Before = before;
            call.Returned = result; call.After = agent.GetCurrentAction(channel).Index;
        }
        return result;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TraceHandIk(Agent agent, in MatrixFrame left, in MatrixFrame right)
    {
        bool result = agent.SetHandInverseKinematicsFrame(in left, in right);
        if (oarScope?.TraceEnabled == true && agent == oarScope.Machine.PilotAgent) oarScope.HandIkCalls++;
        return result;
    }
}
#endif
