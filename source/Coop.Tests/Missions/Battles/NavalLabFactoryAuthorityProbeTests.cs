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

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public sealed class NavalLabFactoryAuthorityProbeTests : IDisposable
{
    private readonly Harmony harmony = new("coop.tests.naval.factory-probe");
    private static NavalLabFactoryAuthorityProbeTests current = null!;
    private readonly HashSet<UIntPtr> activeBodies = new();
    private readonly List<UIntPtr> disables = new();
    private int enables;
    private bool dynamicBody = true;
    private bool validAssignment = true;
    private NavalLabBehavior behavior = null!;

    public NavalLabFactoryAuthorityProbeTests()
    {
        current = this;
        harmony.Patch(AccessTools.Method(typeof(SoundEvent), nameof(SoundEvent.GetEventIdFromString)),
            prefix: new HarmonyMethod(typeof(NavalLabFactoryAuthorityProbeTests), nameof(SoundId)));
        Patch(nameof(GameEntityPhysicsExtensions.DisableDynamicBodySimulation), nameof(Disable));
        Patch(nameof(GameEntityPhysicsExtensions.EnableDynamicBody), nameof(Enable));
        Patch(nameof(GameEntityPhysicsExtensions.HasDynamicRigidBody), nameof(Dynamic));
        Patch(nameof(GameEntityPhysicsExtensions.HasDynamicRigidBodyAndActiveSimulation), nameof(Active));
    }

    private void Patch(string method, string prefix) => harmony.Patch(
        AccessTools.Method(typeof(GameEntityPhysicsExtensions), method, new[] { typeof(WeakGameEntity) }),
        prefix: new HarmonyMethod(typeof(NavalLabFactoryAuthorityProbeTests), prefix));
    private static bool SoundId(ref int __result) { __result = 0; return false; }
    private static bool Disable(WeakGameEntity gameEntity) { current.disables.Add(gameEntity.Pointer); current.activeBodies.Remove(gameEntity.Pointer); return false; }
    private static bool Enable() { current.enables++; return false; }
    private static bool Dynamic(ref bool __result) { __result = current.dynamicBody; return false; }
    private static bool Active(WeakGameEntity gameEntity, ref bool __result) { __result = current.activeBodies.Contains(gameEntity.Pointer); return false; }

    private static T Shell<T>()
    {
        // Engine assemblies are not publicized by Coop.Tests; supply the normal managed script catalog boundary.
        var managed = typeof(ScriptComponentBehavior).BaseType!.Assembly.GetType("TaleWorlds.DotNet.Managed")!;
        var field = managed.GetField("_moduleTypes", BindingFlags.Static | BindingFlags.NonPublic)!;
        var previous = field.GetValue(null);
        try
        {
            if (previous == null) field.SetValue(null, new Dictionary<string, Type>());
            return (T)FormatterServices.GetUninitializedObject(typeof(T));
        }
        finally { field.SetValue(null, previous); }
    }

    private MissionShip Prepare(bool host)
    {
        var id = Guid.NewGuid();
        var manifest = new NavalLabManifest("naval-lab:" + id.ToString("N"), id, new[] { "A", "B" },
            Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray(), new[] { Guid.NewGuid(), Guid.NewGuid() }, NavalLabMode.FactoryAuthorityProbe);
        behavior = new NavalLabBehavior(manifest, "A", null!, null!);
        AccessTools.PropertySetter(typeof(MissionBehavior), nameof(MissionBehavior.Mission)).Invoke(behavior, new object[] { Shell<Mission>() });
        behavior.Ships = new MissionShip[2];
        behavior.factoryAttempted = true;
        behavior.factoryObserving = true;
        behavior.factoryHost = host;
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
    public void CompletedInitialization_RetainsHostAndDisablesFollowerWithoutEnable(bool host)
    {
        var ship = Prepare(host);
        behavior.CompleteFactoryHull(ship);
        Assert.Same(ship, behavior.Ships[0]);
        Assert.Single(behavior.completedFactoryHulls);
        Assert.Equal(host, activeBodies.Contains(new UIntPtr(41)));
        Assert.Equal(host ? 0 : 1, disables.Count);
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

    [Fact]
    public void UnknownPreCompletionFixedEntry_RejectsFollowerWithoutReadingPartialNativeBody()
    {
        Prepare(false);
        behavior.ObserveFactoryFixedTick(Shell<NavalPhysics>(), parallel: true);
        Assert.Equal("factory_probe.follower_fixed_entry_before_complete_or_unattributed", behavior.Blocker);
        Assert.Equal(1, behavior.factoryPreCompletionFixedEntries);
        Assert.Equal(0, behavior.ActiveFixedTicks);
        Assert.Equal(0, behavior.factoryActiveParallelEntries);
        Assert.Empty(disables);
    }

    [Fact]
    public void CompletedInactiveFollower_CountsCallbacksSeparatelyFromActiveAndForceEntries()
    {
        var ship = Prepare(false);
        behavior.CompleteFactoryHull(ship);
        behavior.ObserveFactoryFixedTick(ship.Physics, parallel: false);
        behavior.ObserveFactoryFixedTick(ship.Physics, parallel: true);
        Assert.Equal(1, behavior.FixedTicks);
        Assert.Equal(1, behavior.factoryParallelEntries);
        Assert.Equal(0, behavior.ActiveFixedTicks);
        Assert.Equal(0, behavior.ForceApplications);
        Assert.Null(behavior.Blocker);
        behavior.ObserveFactoryForce();
        Assert.Equal("factory_probe.follower_force_entry", behavior.Blocker);
        Assert.Equal(1, behavior.ForceApplications);
        behavior.Hold();
        behavior.ObserveFactoryForce();
        Assert.Equal(1, behavior.ForceApplications);
    }

    [Fact]
    public void FollowerActiveEntry_RejectsAndTerminalHoldNeverReenablesBody()
    {
        var ship = Prepare(false);
        behavior.CompleteFactoryHull(ship);
        activeBodies.Add(new UIntPtr(41));
        behavior.ObserveFactoryFixedTick(ship.Physics, parallel: false);
        Assert.Equal(1, behavior.ActiveFixedTicks);
        Assert.Equal("factory_probe.follower_active_fixed_entry", behavior.Blocker);
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

    [Theory]
    [InlineData(NavalLabMode.Activation)]
    [InlineData(NavalLabMode.HeldHelm)]
    [InlineData(NavalLabMode.SingleClientNative)]
    public void ProbeObservers_DoNothingForPriorModes(NavalLabMode mode)
    {
        Prepare(false);
        // Only the immutable mode differs; no native boundary may be read by the new observers.
        var id = Guid.NewGuid();
        int owners = mode == NavalLabMode.SingleClientNative ? 1 : 2;
        behavior = new NavalLabBehavior(new NavalLabManifest("naval-lab:" + id.ToString("N"), id, new[] { "A", "B" }.Take(owners).ToArray(),
            Enumerable.Range(0, owners * 5).Select(_ => Guid.NewGuid()).ToArray(), Enumerable.Range(0, owners).Select(_ => Guid.NewGuid()).ToArray(), mode), "A", null!, null!);
        behavior.factoryObserving = true;
        behavior.ObserveFactoryFixedTick(Shell<NavalPhysics>(), parallel: false);
        behavior.ObserveFactoryForce();
        Assert.Equal(0, behavior.FixedTicks);
        Assert.Equal(0, behavior.ForceApplications);
        Assert.Null(behavior.Blocker);
    }

    public void Dispose()
    {
        NavalLabPhysicsPatches.Active = null;
        harmony.UnpatchAll(harmony.Id);
        current = null!;
    }
}
#endif
