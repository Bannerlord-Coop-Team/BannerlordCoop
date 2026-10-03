#if DEBUG
using HarmonyLib;
using Missions.Battles;
using Missions.Naval;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.NavalPhysics;
using NavalDLC.Missions.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using Xunit;
using static Coop.Tests.Missions.Battles.NavalLabTestShells;

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public sealed class NavalLabHullAuthorityTests : IDisposable
{
    private readonly Harmony harmony = new("coop.tests.naval.factory-probe");
    private static NavalLabHullAuthorityTests current = null!;
    private readonly HashSet<UIntPtr> activeBodies = new();
    private readonly List<UIntPtr> disables = new();
    private int enables;
    private bool dynamicBody = true;
    private bool validAssignment = true;
    private NavalLabBehavior behavior = null!;

    public NavalLabHullAuthorityTests()
    {
        current = this;
        harmony.Patch(AccessTools.Method(typeof(SoundEvent), nameof(SoundEvent.GetEventIdFromString)),
            prefix: new HarmonyMethod(typeof(NavalLabHullAuthorityTests), nameof(SoundId)));
        Patch(nameof(GameEntityPhysicsExtensions.DisableDynamicBodySimulation), nameof(Disable));
        Patch(nameof(GameEntityPhysicsExtensions.EnableDynamicBody), nameof(Enable));
        Patch(nameof(GameEntityPhysicsExtensions.HasDynamicRigidBody), nameof(Dynamic));
        Patch(nameof(GameEntityPhysicsExtensions.HasDynamicRigidBodyAndActiveSimulation), nameof(Active));
    }

    private void Patch(string method, string prefix) => harmony.Patch(
        AccessTools.Method(typeof(GameEntityPhysicsExtensions), method, new[] { typeof(WeakGameEntity) }),
        prefix: new HarmonyMethod(typeof(NavalLabHullAuthorityTests), prefix));
    private static bool SoundId(ref int __result) { __result = 0; return false; }
    private static bool Disable(WeakGameEntity gameEntity) { current.disables.Add(gameEntity.Pointer); current.activeBodies.Remove(gameEntity.Pointer); return false; }
    private static bool Enable() { current.enables++; return false; }
    private static bool Dynamic(ref bool __result) { __result = current.dynamicBody; return false; }
    private static bool Active(WeakGameEntity gameEntity, ref bool __result) { __result = current.activeBodies.Contains(gameEntity.Pointer); return false; }


    // The owned slot-zero hull is the active body; a foreign one must stay disabled.
    private MissionShip Prepare(bool owner)
    {
        var id = Guid.NewGuid();
        var manifest = new NavalLabManifest("naval-lab:" + id.ToString("N"), id, new[] { "A", "B" },
            Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray(), new[] { Guid.NewGuid(), Guid.NewGuid() }, NavalLabMode.TwoClientNative);
        behavior = new NavalLabBehavior(manifest, owner ? "A" : "B", null!, null!);
        AccessTools.PropertySetter(typeof(MissionBehavior), nameof(MissionBehavior.Mission)).Invoke(behavior, new object[] { Shell<Mission>() });
        behavior.Ships = new MissionShip[2];
        behavior.factoryAttempted = true;
        behavior.factoryObserving = true;
        behavior.factorySlot = 0;
        behavior.factoryAuthorityValid = () => validAssignment;
        var ship = Shell<MissionShip>();
        var physics = Shell<NavalPhysics>();
        var entity = Activator.CreateInstance(typeof(WeakGameEntity), BindingFlags.Instance | BindingFlags.NonPublic,
            null, new object[] { new UIntPtr(41) }, null);
        var entityField = typeof(ScriptComponentBehavior).GetField("_gameEntity", BindingFlags.NonPublic | BindingFlags.Instance)!;
        entityField.SetValue(ship, entity);
        entityField.SetValue(physics, entity);
        foreach (string name in new[] { "_missionShipObject", "_actuators", "<Formation>k__BackingField", "<ShipOrder>k__BackingField" })
        {
            var field = AccessTools.Field(typeof(MissionShip), name);
            field.SetValue(ship, FormatterServices.GetUninitializedObject(field.FieldType));
        }
        AccessTools.PropertySetter(typeof(NavalPhysics), nameof(NavalPhysics.IsInitialized)).Invoke(physics, new object[] { true });
        AccessTools.Field(typeof(MissionShip), "_physics").SetValue(ship, physics);
        AccessTools.PropertySetter(typeof(MissionShip), nameof(MissionShip.ShipOrigin)).Invoke(ship, new object[] { Shell<NavalLabShipOrigin>() });
        var shipsLogic = new NavalShipsLogic();
        AccessTools.PropertySetter(typeof(MissionBehavior), nameof(MissionBehavior.Mission)).Invoke(shipsLogic, new object[] { behavior.Mission });
        AccessTools.PropertySetter(typeof(MissionShip), nameof(MissionShip.ShipsLogic)).Invoke(ship, new object[] { shipsLogic });
        activeBodies.Add(new UIntPtr(41));
        return ship;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CompletedInitialization_RetainsOwnerAndDisablesForeignHullWithoutEnable(bool owner)
    {
        var ship = Prepare(owner);
        behavior.CompleteFactoryHull(ship);
        Assert.Same(ship, behavior.Ships[0]);
        Assert.Single(behavior.completedFactoryHulls);
        Assert.Equal(owner, activeBodies.Contains(new UIntPtr(41)));
        Assert.Equal(owner ? 0 : 1, disables.Count);
        Assert.Equal(0, enables);
        Assert.False(behavior.factoryMaterialized);
        Assert.False(behavior.factoryReleased);
    }

    [Theory]
    [InlineData("actuators")]
    [InlineData("physics")]
    [InlineData("order")]
    [InlineData("dynamic")]
    public void PartialOrNonDynamicHull_IsNeverDisabledOrRegistered(string missing)
    {
        var ship = Prepare(false);
        if (missing == "actuators") AccessTools.Field(typeof(MissionShip), "_actuators").SetValue(ship, null);
        if (missing == "physics") AccessTools.PropertySetter(typeof(NavalPhysics), nameof(NavalPhysics.IsInitialized)).Invoke(ship.Physics, new object[] { false });
        if (missing == "order") AccessTools.PropertySetter(typeof(MissionShip), nameof(MissionShip.ShipOrder)).Invoke(ship, new object?[] { null });
        if (missing == "dynamic") dynamicBody = false;
        Assert.Throws<InvalidOperationException>(() => behavior.CompleteFactoryHull(ship));
        behavior.Hold();
        Assert.Empty(disables);
        Assert.Empty(behavior.completedFactoryHulls);
    }

    [Fact]
    public void AssignmentLossAtCompletedReturn_HoldsOnlyCompleteHullAndNeverMaterializes()
    {
        var ship = Prepare(true);
        validAssignment = false;
        Assert.Throws<InvalidOperationException>(() => behavior.CompleteFactoryHull(ship));
        Assert.Single(disables);
        Assert.False(behavior.factoryMaterialized);
        Assert.False(activeBodies.Contains(new UIntPtr(41)));
    }

    // Unattributed callbacks must reject the probe without querying an incomplete native body.
    [Fact]
    public void UnknownPreCompletionFixedEntry_RejectsFollowerWithoutReadingPartialNativeBody()
    {
        Prepare(false);
        behavior.ObserveFactoryFixedTick(Shell<NavalPhysics>(), parallel: true);
        Assert.Equal("factory_probe.fixed_entry_before_complete_or_unattributed", behavior.Blocker);
        Assert.Equal(1, behavior.factoryPreCompletionFixedEntries);
        Assert.Equal(0, behavior.ActiveFixedTicks);
        Assert.Equal(0, behavior.factoryActiveParallelEntries);
        Assert.Empty(disables);
    }

    // Attributes callbacks to the completed physics instance, including rejected foreign force attempts.
    [Fact]
    public void CompletedInactiveFollower_CountsCallbacksSeparatelyFromActiveAndForceEntries()
    {
        var ship = Prepare(false);
        behavior.CompleteFactoryHull(ship);
        behavior.ObserveFactoryFixedTick(ship.Physics, parallel: false);
        behavior.ObserveFactoryFixedTick(ship.Physics, parallel: true);
        Assert.Equal(1, behavior.FixedTicks);
        Assert.Equal(1, behavior.factoryParallelEntries);
        Assert.Equal(new long[] { 1, 0 }, behavior.shipFixedEntries);
        Assert.Equal(new long[] { 1, 0 }, behavior.shipParallelEntries);
        Assert.Equal(0, behavior.ActiveFixedTicks);
        Assert.Equal(0, behavior.ForceApplications);
        Assert.Null(behavior.Blocker);
        behavior.ObserveFactoryForce(ship.Physics);
        Assert.Equal("factory_probe.foreign_force_entry:0", behavior.Blocker);
        Assert.Equal(1, behavior.ForceApplications);
        Assert.Equal(new long[] { 1, 0 }, behavior.shipForceEntries);
        Assert.Equal(0, behavior.factoryUnattributedForceEntries);
        behavior.Hold();
        behavior.ObserveFactoryForce(ship.Physics);
        Assert.Equal(1, behavior.ForceApplications);
        Assert.Equal(new long[] { 1, 0 }, behavior.shipForceEntries);
    }

    // A foreign active callback records its slot and terminal hold never reenables the body.
    [Fact]
    public void FollowerActiveEntry_RejectsAndTerminalHoldNeverReenablesBody()
    {
        var ship = Prepare(false);
        behavior.CompleteFactoryHull(ship);
        activeBodies.Add(new UIntPtr(41));
        behavior.ObserveFactoryFixedTick(ship.Physics, parallel: false);
        Assert.Equal(1, behavior.ActiveFixedTicks);
        Assert.Equal("factory_probe.foreign_active_fixed_entry:0", behavior.Blocker);
        Assert.Equal(new long[] { 1, 0 }, behavior.shipActiveFixedEntries);
        behavior.Hold();
        int count = disables.Count;
        behavior.Hold();
        behavior.SetAuthority(true);
        Assert.Equal(count, disables.Count);
        Assert.Equal(0, enables);
    }

    [Fact]
    public void OriginalInitException_IsReturnedUnchangedWithoutCompleteHookOrPartialCleanup()
    {
        var ship = Prepare(false);
        NavalLabPhysicsPatches.Active = behavior;
        var error = new InvalidOperationException("original init failure");
        var patch = typeof(NavalLabPhysicsPatches).GetNestedType("ShipInitialization", BindingFlags.NonPublic)!;
        var result = AccessTools.Method(patch, "Finalizer").Invoke(null, new object[] { ship, ship.ShipsLogic, error });
        Assert.Same(error, result);
        Assert.Empty(disables);
        Assert.Empty(behavior.completedFactoryHulls);
        Assert.Contains("original init failure", behavior.Blocker);
    }

    public void Dispose()
    {
        NavalLabPhysicsPatches.Active = null;
        harmony.UnpatchAll(harmony.Id);
        current = null!;
    }
}
#endif
