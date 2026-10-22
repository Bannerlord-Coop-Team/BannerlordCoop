#if DEBUG
using HarmonyLib;
using Missions.Naval;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.Objects.UsableMachines;
using System.Runtime.CompilerServices;
using System.Threading;
using TaleWorlds.Library;
using Xunit;
using Attachment = NavalDLC.Missions.Objects.UsableMachines.ShipAttachmentMachine.ShipAttachment;
using Joint = NavalDLC.Missions.Objects.UsableMachines.ShipAttachmentMachine.ShipAttachmentJoint;
using RopeState = NavalDLC.Missions.Objects.UsableMachines.ShipAttachmentMachine.ShipAttachment.ShipAttachmentState;

namespace Coop.Tests.Missions.Battles;

/// <summary>
/// R46 freeze: a pilot AI threw at a removed hull through its stale preferred target, and the joint built on landing held no
/// target hull, so its first fixed-tick force step threw. No rope may hook, keep or push a hull that left.
/// </summary>
[Collection("Mission.Current")]
public class NavalRopeRemovalTests
{
    [Fact]
    public void ForgetTarget_ClearsOnlyThePreferencesForTheRemovedHull()
    {
        var removed = Hull();
        var other = Hull();
        var preferringRemoved = Machine(owner: Hull(), preferred: removed);
        var preferringOther = Machine(owner: Hull(), preferred: other);
        var remaining = Hull(preferringRemoved, preferringOther);

        NavalRopes.ForgetTarget(new[] { remaining, other }, removed);

        Assert.Null(preferringRemoved.GetPreferredTargetShip());
        Assert.Same(other, preferringOther.GetPreferredTargetShip());
    }

    [Fact]
    public void AllowConnection_RefusesAPointOnARemovedHull_AndDropsTheStalePreference()
    {
        var removed = Hull();
        removed._isRemoved = true;
        var source = Machine(owner: Hull(), preferred: removed);

        Assert.False(NavalRopes.AllowConnection(source, Point(removed)));
        Assert.Null(source.GetPreferredTargetShip());
    }

    [Fact]
    public void AllowConnection_LetsAnOwnedStationHookALiveHullOrThrowFree()
    {
        var target = Hull();
        var source = Machine(owner: Hull(), preferred: target);

        Assert.True(NavalRopes.AllowConnection(source, Point(target)));
        Assert.True(NavalRopes.AllowConnection(source, null));
        Assert.Same(target, source.GetPreferredTargetShip());
    }

    [Theory]
    [InlineData(false, false, false, false, true)]
    [InlineData(false, true, false, false, false)]
    [InlineData(false, false, true, false, false)]
    [InlineData(true, false, false, false, false)]
    [InlineData(false, false, false, true, false)]
    public void JointHasHulls_OnlyWhileBothHullsAreInTheMission(bool sourceMissing, bool targetMissing, bool targetRemoved,
        bool sourceRemoved, bool expected)
    {
        var source = Hull();
        var target = Hull();
        source._isRemoved = sourceRemoved;
        target._isRemoved = targetRemoved;
        var joint = JointBetween(sourceMissing ? null : source, targetMissing ? null : target);

        Assert.Equal(expected, NavalRopes.JointHasHulls(joint));
    }

    // Review follow-up: the joint guard alone would skip a hull-less joint forever, so the rope tick breaks its rope too.
    [Theory]
    [InlineData(RopeState.RopesPulling, false, false, false, false)]
    [InlineData(RopeState.RopesPulling, true, false, false, true)]
    [InlineData(RopeState.RopeThrown, false, true, false, true)]
    [InlineData(RopeState.BridgeConnected, false, false, true, true)]
    [InlineData(RopeState.BrokenAndWaitingForRemoval, false, true, true, false)]
    public void EndIsGone_ForAnEndThatLeftOrAJointWithoutAHull(RopeState state, bool sourceRemoved, bool targetRemoved,
        bool jointMissingTarget, bool expected)
    {
        var sourceHull = Hull();
        var targetHull = Hull();
        sourceHull._isRemoved = sourceRemoved;
        targetHull._isRemoved = targetRemoved;
        var attachment = (Attachment)RuntimeHelpers.GetUninitializedObject(typeof(Attachment));
        attachment._state = state;
        attachment.AttachmentSource = Machine(sourceHull, preferred: null);
        attachment.AttachmentTarget = Point(targetHull);
        attachment.ShipAttachmentJoint = JointBetween(sourceHull, jointMissingTarget ? null : targetHull);

        Assert.Equal(expected, NavalRopes.EndIsGone(attachment));
    }

    [Fact]
    public void AllowJointStep_SkipsAJointWithoutAHull_AndCountsIt()
    {
        NavalRopes.Clear();

        Assert.True(NavalRopes.AllowJointStep(JointBetween(Hull(), Hull())));
        Assert.False(NavalRopes.AllowJointStep(JointBetween(Hull(), null)));
        Assert.False(NavalRopes.AllowJointStep(JointBetween(Hull(), null)));
        Assert.Equal(2L, Interlocked.Read(ref NavalRopes.jointStepsSkipped));
        NavalRopes.Clear();
    }

    // A constructor-skipped joint between two hulls, either of which may be missing.
    private static Joint JointBetween(MissionShip source, MissionShip target)
    {
        var joint = (Joint)RuntimeHelpers.GetUninitializedObject(typeof(Joint));
        AccessTools.Field(typeof(Joint), nameof(Joint._shipSourceScript)).SetValue(joint, source);
        AccessTools.Field(typeof(Joint), nameof(Joint._shipTargetScript)).SetValue(joint, target);
        return joint;
    }

    // A constructor-skipped hull carrying the given rope stations.
    private static MissionShip Hull(params ShipAttachmentMachine[] machines)
    {
        var hull = ShipTestHulls.Uninitialized<MissionShip>();
        hull._attachmentMachines = new MBList<ShipAttachmentMachine>(machines);
        return hull;
    }

    // A constructor-skipped rope station on the owner hull whose AI prefers the given hull.
    private static ShipAttachmentMachine Machine(MissionShip owner, MissionShip preferred)
    {
        var machine = ShipTestHulls.Uninitialized<ShipAttachmentMachine>();
        machine.OwnerShip = owner;
        machine.SetPreferredTargetShip(preferred);
        return machine;
    }

    // A constructor-skipped attachment point on the hull.
    private static ShipAttachmentPointMachine Point(MissionShip hull)
    {
        var point = ShipTestHulls.Uninitialized<ShipAttachmentPointMachine>();
        point.OwnerShip = hull;
        return point;
    }
}
#endif
