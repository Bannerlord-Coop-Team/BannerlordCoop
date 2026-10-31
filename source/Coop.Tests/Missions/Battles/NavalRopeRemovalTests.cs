#if COOP_NAVAL
using HarmonyLib;
using Missions.Naval;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.Objects.UsableMachines;
using System.Runtime.CompilerServices;
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
    public void ForgetRemovedHull_ClearsOnlyThePreferencesForTheRemovedHull_AndItsRecords()
    {
        var removed = Hull();
        var other = Hull();
        var preferringRemoved = Machine(owner: Hull(), preferred: removed);
        var preferringOther = Machine(owner: Hull(), preferred: other);
        var remaining = Hull(preferringRemoved, preferringOther);

        var removedStation = Machine(owner: removed, preferred: null);
        removed._attachmentMachines.Add(removedStation);
        NavalRopes.Clear();
        NavalRopes.records[removedStation] = new NavalRopes.RopeRecord { Generation = 2 };
        NavalRopes.records[preferringOther] = new NavalRopes.RopeRecord { Generation = 1 };

        NavalRopeRemoval.ForgetRemovedHull(new[] { remaining, other }, removed);

        Assert.Null(preferringRemoved.GetPreferredTargetShip());
        Assert.Same(other, preferringOther.GetPreferredTargetShip());
        Assert.Null(NavalRopes.RecordOf(removedStation));
        Assert.NotNull(NavalRopes.RecordOf(preferringOther));
        NavalRopes.Clear();
    }

    [Fact]
    public void AllowConnection_RefusesAPointOnARemovedHull()
    {
        var removed = Hull();
        removed._isRemoved = true;
        var source = Machine(owner: Hull(), preferred: removed);

        Assert.False(NavalRopes.AllowConnection(source, Point(removed)));
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

        Assert.Equal(expected, NavalRopeRemoval.JointHasHulls(joint));
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

        Assert.Equal(expected, NavalRopeRemoval.EndIsGone(attachment));
    }

    [Fact]
    public void AllowJointStep_SkipsAJointWithoutAHull_AndCountsIt()
    {
        NavalRopes.Clear();

        Assert.True(NavalRopeRemoval.AllowJointStep(JointBetween(Hull(), Hull())));
        Assert.False(NavalRopeRemoval.AllowJointStep(JointBetween(Hull(), null)));
        Assert.False(NavalRopeRemoval.AllowJointStep(JointBetween(Hull(), null)));
        Assert.Equal(2L, NavalRopeRemoval.JointStepsSkipped);
        NavalRopes.Clear();
    }

    // S17 A1 / R49: vanilla's removal broke every rope in the battle; in co-op only a rope with an end on the removed hull breaks.
    [Theory]
    [InlineData(true, "source", true)]
    [InlineData(true, "target", true)]
    [InlineData(true, "other", false)]
    [InlineData(true, "flying", false)]
    [InlineData(false, "other", true)]
    public void BreaksOnRemoval_OnlyARopeWithAnEndOnTheRemovedHull(bool coopBattle, string removedEnd, bool expected)
    {
        var source = Hull();
        var target = Hull();
        var removed = removedEnd == "source" ? source : removedEnd == "target" ? target : Hull();
        var rope = Rope(source, removedEnd == "flying" ? null : target);
        bool wasActive = CoopNavalMissionScope.IsActive;
        try
        {
            CoopNavalMissionScope.IsActive = coopBattle;

            Assert.Equal(expected, NavalRopeRemoval.BreaksOnRemoval(rope, removed));
        }
        finally
        {
            CoopNavalMissionScope.IsActive = wasActive;
        }
    }

    // A constructor-skipped rope from a station on the source hull, hooked to a point on the target hull when one is given.
    internal static Attachment Rope(MissionShip source, MissionShip target)
    {
        var rope = (Attachment)RuntimeHelpers.GetUninitializedObject(typeof(Attachment));
        rope._state = RopeState.RopesPulling;
        rope.AttachmentSource = Machine(source, preferred: null);
        rope.AttachmentTarget = target == null ? null : Point(target);
        return rope;
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
    internal static MissionShip Hull(params ShipAttachmentMachine[] machines)
    {
        var hull = ShipTestHulls.Uninitialized<MissionShip>();
        hull._attachmentMachines = new MBList<ShipAttachmentMachine>(machines);
        return hull;
    }

    // A constructor-skipped rope station on the owner hull whose AI prefers the given hull.
    internal static ShipAttachmentMachine Machine(MissionShip owner, MissionShip preferred)
    {
        var machine = ShipTestHulls.Uninitialized<ShipAttachmentMachine>();
        machine.OwnerShip = owner;
        machine.SetPreferredTargetShip(preferred);
        return machine;
    }

    // A constructor-skipped attachment point on the hull.
    internal static ShipAttachmentPointMachine Point(MissionShip hull)
    {
        var point = ShipTestHulls.Uninitialized<ShipAttachmentPointMachine>();
        point.OwnerShip = hull;
        return point;
    }
}
#endif
