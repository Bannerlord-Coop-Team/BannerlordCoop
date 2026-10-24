#if COOP_NAVAL
using HarmonyLib;
using Missions.Naval;
using NavalDLC.Missions.NavalPhysics;
using NavalDLC.Missions.Objects;
using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.Core;
using Xunit;

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public class NavalRopePatchesTests
{
    [Theory]
    [InlineData(typeof(RopeConnectPatch))]
    [InlineData(typeof(RopeDisconnectPatch))]
    [InlineData(typeof(RopeStatePatch))]
    [InlineData(typeof(RopePlankEligibilityPatch))]
    [InlineData(typeof(RopeBreakCheckPatch))]
    [InlineData(typeof(RopeReplicaPlankFlightPatch))]
    [InlineData(typeof(RopeReplicaTickPatch))]
    [InlineData(typeof(RopeRetargetPatch))]
    [InlineData(typeof(RopeThrowCurvePatch))]
    [InlineData(typeof(RopeForcePatch))]
    [InlineData(typeof(RopePlankCosmeticsPatch))]
    public void Patch_BindsToTheInstalledNavalDlc(Type patch)
    {
        var harmony = new Harmony("coop.tests.naval.ropes." + patch.Name);
        try
        {
            Assert.NotEmpty(harmony.CreateClassProcessor(patch).Patch());
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    [Fact]
    public void ForceTranspiler_RoutesEveryNativeJointForceWrite()
    {
        int routed = 0;
        foreach (var method in RopeForcePatch.TargetMethods())
        {
            var instrumented = RopeForcePatch.Transpiler(PatchProcessor.GetOriginalInstructions(method)).ToArray();

            Assert.DoesNotContain(instrumented, code => code.operand is MethodInfo called && called.DeclaringType == typeof(NavalPhysics)
                && called.Name.StartsWith("Apply", StringComparison.Ordinal));
            routed += instrumented.Count(code => code.operand is MethodInfo called && called.DeclaringType == typeof(RopeForcePatch));
        }

        Assert.True(routed >= 2);
    }

    [Fact]
    public void AllowForce_WritesJointForceOnlyToSimulatedHulls_AndCountsEachKind()
    {
        var copyPhysics = ShipTestHulls.Uninitialized<NavalPhysics>();
        var copy = ShipTestHulls.Uninitialized<MissionShip>();
        copy._physics = copyPhysics;
        bool wasActive = CoopNavalMissionScope.IsActive;
        NavalRopes.Clear();
        try
        {
            CoopNavalMissionScope.IsActive = true;
            NavalForeignHulls.Add(copy, null);

            Assert.False(RopeForcePatch.AllowForce(copyPhysics, 1));
            Assert.True(RopeForcePatch.AllowForce(ShipTestHulls.Uninitialized<NavalPhysics>(), 0));
            Assert.Equal(new long[] { 0, 1, 0 }, RopeForcePatch.ForeignForcesFiltered());
            Assert.Equal(new long[] { 1, 0, 0 }, RopeForcePatch.OwnForceWrites());
        }
        finally
        {
            NavalForeignHulls.Remove(copy);
            CoopNavalMissionScope.IsActive = wasActive;
            NavalRopes.Clear();
        }
    }

    [Fact]
    public void ForceTranspiler_WithoutNativeCallsites_FailsClosed()
    {
        Assert.Throws<InvalidOperationException>(() => RopeForcePatch.Transpiler(Array.Empty<CodeInstruction>()).ToArray());
    }

    [Fact]
    public void CosmeticsTranspiler_DrawsPlankRandomnessFromTheSharedSeed()
    {
        foreach (var method in RopePlankCosmeticsPatch.TargetMethods())
        {
            var instrumented = RopePlankCosmeticsPatch.Transpiler(PatchProcessor.GetOriginalInstructions(method)).ToArray();

            Assert.DoesNotContain(instrumented, code => code.operand is MethodInfo called && called.DeclaringType == typeof(MBRandom));
        }
    }
}
#endif
