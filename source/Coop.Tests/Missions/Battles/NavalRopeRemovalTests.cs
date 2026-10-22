#if DEBUG
using HarmonyLib;
using Missions.Naval;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.Objects.UsableMachines;
using System.Runtime.CompilerServices;
using TaleWorlds.Library;
using Xunit;
using Joint = NavalDLC.Missions.Objects.UsableMachines.ShipAttachmentMachine.ShipAttachmentJoint;

namespace Coop.Tests.Missions.Battles;

/// <summary>
/// R46 freeze: a pilot AI threw at a removed hull through its stale preferred target, and the joint built on landing held no
/// target hull, so its first fixed-tick force step threw. No rope may hook, keep or push a hull that left.
/// </summary>
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
        var joint = (Joint)RuntimeHelpers.GetUninitializedObject(typeof(Joint));
        AccessTools.Field(typeof(Joint), nameof(Joint._shipSourceScript)).SetValue(joint, sourceMissing ? null : source);
        AccessTools.Field(typeof(Joint), nameof(Joint._shipTargetScript)).SetValue(joint, targetMissing ? null : target);

        Assert.Equal(expected, NavalRopes.JointHasHulls(joint));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void AllowsTarget_NeverAHullThatLeft(bool targetHullGone, bool expected)
    {
        Assert.Equal(expected, NavalRopes.AllowsTarget(targetHullGone));
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
