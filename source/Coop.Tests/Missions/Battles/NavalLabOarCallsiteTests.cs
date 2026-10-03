#if DEBUG
using System;
using System.Linq;
using System.Runtime.Serialization;
using Missions.Messages;
using TaleWorlds.Library;
using HarmonyLib;
using Missions.Naval;
using NavalDLC.Missions.Objects.UsableMachines;
using NavalDLC.Missions.ShipActuators;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public sealed class NavalLabOarCallsiteTests
{
    [Fact]
    public void InstalledMachineShapeKeepsGetterCallsAndWrapsEachNativeWriteOnce()
    {
        var original = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(ShipOarMachine), "OnTickParallel2")).ToArray();
        var instrumented = NavalLabPresentationPatches.InstrumentOarCallsites(original).ToArray();
        Assert.Equal(original.Length + 24, instrumented.Length);
        Assert.Equal(6, instrumented.Count(code => code.Calls(AccessTools.Method(typeof(NavalLabPresentationPatches), "TraceSetAction"))));
        Assert.Single(instrumented.Where(code => code.Calls(AccessTools.Method(typeof(NavalLabPresentationPatches), "TraceHandIk"))));
        Assert.Single(instrumented.Where(code => code.Calls(AccessTools.Method(typeof(MissionOar), nameof(MissionOar.IsInRowingMotion)))));
        Assert.DoesNotContain(instrumented, code => code.Calls(AccessTools.Method(typeof(Agent), nameof(Agent.SetActionChannel))));
    }

    [Fact]
    public void MissingCallsitesFailClosedInsteadOfPublishingMadeUpObservations()
    {
        Assert.Throws<InvalidOperationException>(() => NavalLabPresentationPatches.InstrumentOarCallsites(Array.Empty<CodeInstruction>()).ToArray());
    }

    [Fact]
    public void StackObserversReturnOriginalValuesAndSnapshotDoesNotChangeAfterPublication()
    {
        var previous = NavalLabPresentationPatches.oarScope;
        try
        {
            var scope = new NavalLabPresentationPatches.OarScope(null!, null!, 17) { TraceEnabled = true, Ordinal = 3 };
            NavalLabPresentationPatches.oarScope = scope;
            Assert.False(NavalLabPresentationPatches.ConsumeMotion(false));
            Assert.True(NavalLabPresentationPatches.ConsumeExtracted(true));
            Assert.Equal(-1, NavalLabPresentationPatches.ConsumeRate(-1));
            Assert.Equal(2, NavalLabPresentationPatches.ConsumePhase(2));
            var snapshot = scope.Snapshot();
            Assert.True(NavalLabPresentationPatches.ConsumeMotion(true));
            var saved = JObject.FromObject(snapshot);
            Assert.False((bool)saved["rowing"]!);
            Assert.Equal(1, (int)saved["motionReads"]!);
            Assert.Equal(17, (long)saved["sourceSequence"]!);
            Assert.Equal(JTokenType.Null, saved["channels"]![0]!.Type);
            Assert.True(float.IsNaN(NavalLabPresentationPatches.ConsumeRate(float.NaN)));
            Assert.DoesNotContain("NaN", JsonConvert.SerializeObject(scope.Snapshot()));
        }
        finally { NavalLabPresentationPatches.oarScope = previous; }
    }

    [Fact]
    public void ScopedNativeZeroFalseInputsSelectBackwardRowingWithoutChangingNativeState()
    {
        var previous = NavalLabPresentationPatches.oarScope;
        try
        {
            var oar = (MissionOar)FormatterServices.GetUninitializedObject(typeof(MissionOar));
            AccessTools.Field(typeof(MissionOar), "_sidePhaseData").SetValue(oar,
                new OarSidePhaseController(null!, OarSidePhaseController.OarSide.Left));
            var state = new NetworkNavalLabOarPresentation("committed-oar", 0, 2.5f, 1, -1, true,
                NetworkNavalLabOarPresentation.FromFrame(MatrixFrame.Identity));
            var scope = new NavalLabPresentationPatches.OarScope(null!, state, 1043) { TraceEnabled = true };
            NavalLabPresentationPatches.oarScope = scope;
            Assert.Equal(0, oar.NeededRevolutionRate);
            Assert.False(oar.IsInRowingMotion());
            Assert.False(oar.IsExtracted);
            // These are the same reads and branch predicates in the installed machine body.
            Assert.True(NavalLabPresentationPatches.ConsumeExtracted(oar.IsExtracted));
            Assert.True(NavalLabPresentationPatches.ConsumeRate(oar.NeededRevolutionRate) < 0);
            Assert.Equal(2.5f, NavalLabPresentationPatches.ConsumePhase(oar.VisualPhase));
            Assert.True(NavalLabPresentationPatches.ConsumeMotion(oar.IsInRowingMotion()));
            var trace = JObject.FromObject(scope.Snapshot());
            Assert.Equal(0, (float)trace["rawNeededRate"]!);
            Assert.Equal(-1, (float)trace["neededRate"]!);
            Assert.False((bool)trace["rawRowing"]!);
            Assert.True((bool)trace["rowing"]!);
            Assert.Equal(1, (int)trace["rateReads"]!);
            Assert.Equal(1, (int)trace["motionReads"]!);
            Assert.Equal(0, oar.NeededRevolutionRate);
            Assert.False(oar.IsInRowingMotion());
            Assert.False(oar.IsExtracted);
        }
        finally { NavalLabPresentationPatches.oarScope = previous; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingPresentationOrFailedStationAdmissionKeepsNativeBranchValues(bool hasPresentation)
    {
        var previous = NavalLabPresentationPatches.oarScope;
        try
        {
            var state = hasPresentation ? new NetworkNavalLabOarPresentation("committed-oar", 0, 2.5f, 1, -1, true,
                NetworkNavalLabOarPresentation.FromFrame(MatrixFrame.Identity)) : null;
            NavalLabPresentationPatches.oarScope = new NavalLabPresentationPatches.OarScope(null!, state!, 1043)
                { TraceEnabled = !hasPresentation };
            Assert.Equal(0, NavalLabPresentationPatches.ConsumeRate(0));
            Assert.False(NavalLabPresentationPatches.ConsumeMotion(false));
            Assert.False(NavalLabPresentationPatches.ConsumeExtracted(false));
            NavalLabPresentationPatches.oarScope = null!;
            Assert.Equal(0, NavalLabPresentationPatches.ConsumeRate(0));
            Assert.False(NavalLabPresentationPatches.ConsumeMotion(false));
        }
        finally { NavalLabPresentationPatches.oarScope = previous; }
    }

    [Fact]
    public void InstalledBranchConsumesWrapperResultImmediatelyAfterEachOriginalGetter()
    {
        var code = NavalLabPresentationPatches.InstrumentOarCallsites(
            PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(ShipOarMachine), "OnTickParallel2"))).ToArray();
        foreach (var pair in new[] { ("get_NeededRevolutionRate", "ConsumeRate"), ("get_VisualPhase", "ConsumePhase"),
            ("get_IsExtracted", "ConsumeExtracted"), ("IsInRowingMotion", "ConsumeMotion") })
        {
            var method = AccessTools.Method(typeof(MissionOar), pair.Item1);
            var wrapper = AccessTools.Method(typeof(NavalLabPresentationPatches), pair.Item2);
            foreach (int index in Enumerable.Range(0, code.Length).Where(i => code[i].Calls(method)))
                Assert.True(code[index + 1].Calls(wrapper));
        }
    }

    // Verifies every installed stop remains reachable through one wrapper with its own ordinal.
    [Fact]
    public void InstalledStopCallsitesKeepOriginalReadsAndDistinctStopOrdinals()
    {
        var original = PatchProcessor.GetOriginalInstructions(AccessTools.Method(typeof(ShipOarMachine), "OnTickParallel2")).ToArray();
        var code = NavalLabPresentationPatches.InstrumentOarStopInputs(original).ToArray();
        var stop = AccessTools.Method(typeof(Agent), nameof(Agent.StopUsingGameObjectMT));
        var wrapper = AccessTools.Method(typeof(NavalLabPresentationPatches), "TraceStationStop");
        var indices = Enumerable.Range(0, code.Length).Where(i => code[i].Calls(wrapper)).ToArray();
        Assert.Equal(3, indices.Length);
        Assert.Equal(new[] { 0, 1, 2 }, indices.Select(i => (int)code[i - 1].operand));
        Assert.All(indices, i => Assert.Equal(System.Reflection.Emit.OpCodes.Ldc_I4, code[i - 1].opcode));
        Assert.DoesNotContain(code, instruction => instruction.Calls(stop));
        Assert.Equal(original.Where(instruction => !instruction.Calls(stop)), code.Where(instruction => original.Contains(instruction)));
        Assert.Throws<InvalidOperationException>(() => NavalLabPresentationPatches.InstrumentOarStopInputs(
            original.Where(instruction => !instruction.Calls(stop))).ToArray());
    }

    // Checks that stop diagnostics preserve stack values and publish an immutable, partial snapshot.
    [Fact]
    public void StopObserversPreserveValuesAndLeaveShortCircuitedInputsAbsent()
    {
        var previous = NavalLabPresentationPatches.oarScope;
        try
        {
            var inputs = new NavalLabPresentationPatches.StopInputs();
            NavalLabPresentationPatches.oarScope = new NavalLabPresentationPatches.OarScope(null!, null!, 0) { StopInputs = inputs };
            Assert.True(NavalLabPresentationPatches.ObserveStopSitting(true));
            Assert.False(NavalLabPresentationPatches.ObserveStopStruck(false));
            Assert.Equal((AgentMovementLockedState)0, NavalLabPresentationPatches.ObserveStopMovement((AgentMovementLockedState)0));
            Assert.False(NavalLabPresentationPatches.ObserveStopAlternative(false));
            Assert.True(float.IsNaN(NavalLabPresentationPatches.ObserveStopRemovalTime(float.NaN)));
            Assert.Equal(12f, NavalLabPresentationPatches.ObserveStopMissionTime(12f));
            var snapshot = inputs.Snapshot();
            NavalLabPresentationPatches.ObserveStopAlternative(true);
            var saved = JObject.FromObject(snapshot);
            Assert.Single((JArray)saved["alternatives"]!);
            Assert.False((bool)saved["alternatives"]![0]!);
            Assert.Empty((JArray)saved["currentActions"]!);
            Assert.Equal(JTokenType.Null, saved["removalTimes"]![0]!.Type);
            Assert.Null(NavalLabPresentationPatches.StationStopCallsite(null!));
            NavalLabPresentationPatches.oarScope = null!;
            Assert.False(NavalLabPresentationPatches.ObserveStopSitting(false));
            Assert.True(NavalLabPresentationPatches.ObserveStopAlternative(true));
        }
        finally { NavalLabPresentationPatches.oarScope = previous; }
    }

    [Fact]
    public void DiagnosticMachinePatchCanBeInstalledWithoutRunningNativeTick()
    {
        var harmony = new Harmony("coop.tests.naval.oar-callsite-shape");
        try { harmony.CreateClassProcessor(typeof(NavalLabPresentationPatches.MachinePresentationScope)).Patch(); }
        finally { harmony.UnpatchAll(harmony.Id); }
    }
}
#endif
