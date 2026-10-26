#if COOP_NAVAL
using HarmonyLib;
using Missions.Naval;
using NavalDLC.Missions.NavalPhysics;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.Objects.UsableMachines;
using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit;
using Joint = NavalDLC.Missions.Objects.UsableMachines.ShipAttachmentMachine.ShipAttachmentJoint;
using RopeState = NavalDLC.Missions.Objects.UsableMachines.ShipAttachmentMachine.ShipAttachment.ShipAttachmentState;

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public class NavalRopePatchesTests
{
    [Theory]
    [InlineData(typeof(RopeConnectPatch))]
    [InlineData(typeof(RopeShipRemovalPatch))]
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
    public void RemovalTranspiler_FiltersBothNativeRopeLoops()
    {
        var method = AccessTools.DeclaredMethod(typeof(MissionShip), nameof(MissionShip.OnShipRemoved));

        var instrumented = RopeShipRemovalPatch.Transpiler(PatchProcessor.GetOriginalInstructions(method)).ToArray();

        Assert.DoesNotContain(instrumented, code => code.operand is MethodInfo called && called.Name == "get_CurrentAttachment");
        Assert.Contains(instrumented, code => code.operand is MethodInfo called && called.Name == nameof(RopeShipRemovalPatch.StationRopeBroken));
        Assert.Contains(instrumented, code => code.operand is MethodInfo called && called.Name == nameof(RopeShipRemovalPatch.PointRopeBroken));
    }

    [Fact]
    public void RemovalTranspiler_WithoutTheNativeLoops_FailsClosed()
    {
        Assert.Throws<InvalidOperationException>(() => RopeShipRemovalPatch.Transpiler(Array.Empty<CodeInstruction>()).ToArray());
    }

    // R49 B4: one hull leaving broke every unrelated rope, because vanilla's OnShipRemoved runs on every hull.
    [Fact]
    public void OnShipRemoved_InACoopBattle_KeepsARopeBetweenTwoOtherHulls()
    {
        var other = NavalRopeRemovalTests.Hull();
        var hull = NavalRopeRemovalTests.Hull();
        var rope = NavalRopeRemovalTests.Rope(hull, other);
        var station = rope.AttachmentSource;
        station.CurrentAttachment = rope;
        hull._attachmentMachines = new MBList<ShipAttachmentMachine> { station };
        hull._attachmentPointMachines = new MBList<ShipAttachmentPointMachine>();
        hull._shipEventListeners = new MBList<IShipEventListener>();

        var harmony = new Harmony("coop.tests.naval.ropes.removal");
        bool wasActive = CoopNavalMissionScope.IsActive;
        try
        {
            harmony.CreateClassProcessor(typeof(RopeShipRemovalPatch)).Patch();
            CoopNavalMissionScope.IsActive = true;

            // The method's tail reaches the hull's actuators, which can't be built without the engine, after both rope loops.
            Assert.Throws<NullReferenceException>(() => hull.OnShipRemoved(NavalRopeRemovalTests.Hull()));

            // Vanilla's loop sets the broken state first, then destroys the rope.
            Assert.Equal(RopeState.RopesPulling, rope.State);
            Assert.Same(rope, station.CurrentAttachment);
        }
        finally
        {
            CoopNavalMissionScope.IsActive = wasActive;
            harmony.UnpatchAll(harmony.Id);
        }
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

    // S17 A3: R46 roped copies rolled off (roll sd 1.7-4.4 degrees, gain up to 1.56) because the joint's roll spring was
    // filtered on them; it is a torque about each hull's own forward axis, so it applies to a copy as on its owner.
    [Fact]
    public void ForceGate_LetsTheJointRollStabilizerReachCopies()
    {
        var stabilizer = AccessTools.DeclaredMethod(typeof(Joint), nameof(Joint.StabilizeShipUps));

        Assert.DoesNotContain(stabilizer, RopeForcePatch.TargetMethods());
        Assert.Contains(PatchProcessor.GetOriginalInstructions(stabilizer), code => code.operand is MethodInfo called
            && called.DeclaringType == typeof(NavalPhysics) && called.Name == nameof(NavalPhysics.ApplyTorque));
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
