#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Missions.Messages;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.Objects.UsableMachines;
using NavalDLC.Missions.ShipActuators;
using TaleWorlds.Library;

namespace Missions.Naval;

internal static partial class NavalLabPresentationPatches
{
    private sealed class OarScope
    {
        internal readonly ShipOarMachine Machine;
        internal readonly NetworkNavalLabOarPresentation State;
        internal readonly long Sequence;
        internal bool TraceEnabled;
        internal Guid Combatant;
        internal string Controller;
        internal long Ordinal;
        internal int ExtractedReads, RateReads, PhaseReads, MotionReads, BladeFrameCalls, HandIkCalls;
        internal bool? Extracted, Rowing, RawExtracted, RawRowing;
        internal float? Rate, Phase, RawRate, RawPhase;
        internal NetworkNavalLabOarPresentation PresentationState => TraceEnabled ? State : null;
        internal readonly ActionCall[] Actions = new ActionCall[2];
        internal object Snapshot() => !TraceEnabled ? null : new
        {
            callbackOrdinal = Ordinal, sourceSequence = Sequence, combatantId = Combatant, nativeController = Controller,
            hasScopedPresentation = PresentationState != null,
            rawExtracted = RawExtracted, rawNeededRate = RawRate, rawPhase = RawPhase, rawRowing = RawRowing,
            extractedReads = ExtractedReads, extracted = Extracted, rateReads = RateReads, neededRate = Rate,
            phaseReads = PhaseReads, phase = Phase, motionReads = MotionReads, rowing = Rowing,
            bladeFrameCalls = BladeFrameCalls, handIkCalls = HandIkCalls,
            channels = Actions.Select((action, channel) => action == null ? null : (object)new
            { channel, calls = action.Calls, requestedAction = action.Requested, actionBefore = action.Before,
                returned = action.Returned, actionAfter = action.After }).ToArray()
        };
        internal OarScope(ShipOarMachine machine, NetworkNavalLabOarPresentation state, long sequence) { Machine = machine; State = state; Sequence = sequence; }
    }
    private sealed class ActionCall
    {
        internal int Calls, Requested, Before, After;
        internal bool Returned;
    }
    [ThreadStatic] private static OarScope oarScope;
    private static NetworkNavalLabOarPresentation OarState(MissionOar oar) => oarScope?.Machine._oar == oar ? oarScope.State : null;

    [HarmonyPatch(typeof(ShipOarMachine), "OnTickParallel2")]
    private static class MachinePresentationScope
    {
        private static void Prefix(ShipOarMachine __instance, out OarScope __state)
        {
            __state = oarScope;
            long sequence = 0;
            var state = NavalLabPhysicsPatches.Active?.FindOarPresentation(__instance, out sequence);
            oarScope = new OarScope(__instance, state, sequence);
            var active = NavalLabPhysicsPatches.Active;
            if (active != null) oarScope.TraceEnabled = active.BeginOarCallsiteTrace(__instance,
                out oarScope.Combatant, out oarScope.Controller, out oarScope.Ordinal);
        }
        private static void Postfix(ShipOarMachine __instance) => NavalLabPhysicsPatches.Active?.RecordOarPresentation(
            __instance, oarScope.Sequence, oarScope.Snapshot(), oarScope.Phase, oarScope.Rate, oarScope.Rowing, oarScope.PresentationState?.Extraction);
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => InstrumentOarCallsites(instructions);
        private static Exception Finalizer(Exception __exception, OarScope __state) { oarScope = __state; return __exception; }
    }

    [HarmonyPatch(typeof(MissionOar), nameof(MissionOar.ComputeOarEntityFrame))]
    private static class BladeLocalFrame
    {
        private static bool Prefix(MissionOar __instance, ref MatrixFrame __result)
        {
            var state = OarState(__instance);
            if (state == null) return true;
            __result = NetworkNavalLabOarPresentation.ToFrame(state.BladeFrame);
            return false;
        }
    }
    [HarmonyPatch(typeof(MissionSail), "UpdateSailRotationVisual")]
    private static class SailDerivedYaw
    {
        private static bool Prefix(MissionSail __instance) => NavalLabPhysicsPatches.Active?.FindSailPresentation(__instance._sailVisual) == null;
    }
    [HarmonyPatch(typeof(MissionSail), "UpdateSailSetting")]
    private static class SailDerivedSetting
    {
        private static bool Prefix(MissionSail __instance) => NavalLabPhysicsPatches.Active?.FindSailPresentation(__instance._sailVisual) == null;
    }
    [HarmonyPatch(typeof(MissionSail), "UpdateSailVisuals")]
    private static class SailYawAndOpening
    {
        private static bool Prefix(MissionSail __instance, float dt)
        {
            if (NavalLabPhysicsPatches.Active?.PresentSail(__instance) != true) return true;
            // Retain local cloth wind only, without importing a force record or running the actuator solver.
            __instance.UpdateForcedWindOfSailsAndTopBanner(dt);
            return false;
        }
    }
    [HarmonyPatch(typeof(SailVisual), "CheckFoldAnimationState")]
    private static class SailFoldClock
    {
        private static void Postfix(SailVisual __instance, long __state) => NavalLabPhysicsPatches.Active?.RecordSailPresentation(__instance, __state);
        private static bool Prefix(SailVisual __instance, out long __state)
        {
            __state = 0;
            var state = NavalLabPhysicsPatches.Active?.FindSailPresentation(__instance, out __state);
            if (state == null) return true;
            __instance.SailEnabled = state.Enabled;
            __instance._ongoingAnimationData.FoldIsOngoing = state.Folding;
            __instance._ongoingAnimationData.UnfoldIsOngoing = state.Unfolding;
            __instance._ongoingAnimationData.CurrentProgress = state.Progress;
            __instance._ongoingAnimationData.RealProgress = state.RealProgress;
            if (state.Folding) __instance.TickFoldAnimation(0);
            else if (state.Unfolding) __instance.TickUnfoldAnimation(0);
            else if (state.Enabled)
            {
                // A first baseline may arrive after unfolding finished, with no local transition history.
                __instance._ongoingAnimationData.CurrentProgress = __instance._unfoldSailDuration
                    + __instance._foldFreeBoneResetDuration + __instance._foldedSailTransitionDuration - 0.0001f;
                __instance.TickUnfoldAnimation(0);
                __instance._ongoingAnimationData.CurrentProgress = state.Progress;
            }
            return false;
        }
    }
}
#endif
