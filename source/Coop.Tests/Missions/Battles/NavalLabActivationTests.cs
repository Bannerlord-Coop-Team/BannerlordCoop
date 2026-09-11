#if DEBUG
using HarmonyLib;
using Missions.Battles;
using Missions.Naval;
using NavalDLC.Missions.Objects;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using TaleWorlds.Engine;
using Xunit;

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public sealed class NavalLabActivationTests : IDisposable
{
    private readonly Harmony harmony = new("coop.tests.naval.activation");
    private readonly NavalLabBehavior behavior;
    private static NavalLabActivationTests current = null!;
    private readonly HashSet<UIntPtr> activeBodies = new();
    private readonly List<UIntPtr> enables = new();
    private readonly List<UIntPtr> disables = new();
    private bool enableSucceeds = true;
    private bool hasDynamicBody = true;
    private bool dynamicBodyThrows;
    private bool bodyFlagThrows;
    private int diagnosticReads;
    private readonly List<string> mutationBoundaries = new();

    public NavalLabActivationTests()
    {
        current = this;
        harmony.Patch(AccessTools.Method(typeof(SoundEvent), nameof(SoundEvent.GetEventIdFromString)),
            prefix: new HarmonyMethod(typeof(NavalLabActivationTests), nameof(SoundId)));
        var id = Guid.NewGuid();
        var manifest = new NavalLabManifest("naval-lab:" + id.ToString("N"), id, new[] { "A", "B" },
            Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray(), new[] { Guid.NewGuid(), Guid.NewGuid() });
        behavior = new NavalLabBehavior(manifest, "A", null!, null!);
        var managed = typeof(ScriptComponentBehavior).BaseType!.Assembly.GetType("TaleWorlds.DotNet.Managed")!;
        var moduleTypes = managed.GetField("_moduleTypes", BindingFlags.Static | BindingFlags.NonPublic)!;
        var previous = moduleTypes.GetValue(null);
        try
        {
            if (previous == null) moduleTypes.SetValue(null, new Dictionary<string, Type>());
            behavior.Ships = new[] { Ship(1), Ship(2) };
        }
        finally { moduleTypes.SetValue(null, previous); }
        Patch(nameof(GameEntityPhysicsExtensions.EnableDynamicBody), nameof(Enable));
        Patch(nameof(GameEntityPhysicsExtensions.DisableDynamicBodySimulation), nameof(Disable));
        Patch(nameof(GameEntityPhysicsExtensions.HasDynamicRigidBodyAndActiveSimulation), nameof(IsActive));
        Patch(nameof(GameEntityPhysicsExtensions.HasDynamicRigidBody), nameof(HasDynamicBody));
        Patch(nameof(GameEntityPhysicsExtensions.GetPhysicsState), nameof(PhysicsState));
        harmony.Patch(AccessTools.PropertyGetter(typeof(WeakGameEntity), nameof(WeakGameEntity.BodyFlag)),
            prefix: new HarmonyMethod(typeof(NavalLabActivationTests), nameof(BodyFlag)));
    }

    private void Patch(string method, string prefix) => harmony.Patch(
        AccessTools.Method(typeof(GameEntityPhysicsExtensions), method, new[] { typeof(WeakGameEntity) }),
        prefix: new HarmonyMethod(typeof(NavalLabActivationTests), prefix));

    private static MissionShip Ship(uint pointer)
    {
        // Engine internals are not publicized in this project; only identity shells are needed.
        var ship = (MissionShip)FormatterServices.GetUninitializedObject(typeof(MissionShip));
        var entity = Activator.CreateInstance(typeof(WeakGameEntity), BindingFlags.Instance | BindingFlags.NonPublic,
            null, new object[] { new UIntPtr(pointer) }, null);
        typeof(ScriptComponentBehavior).GetField("_gameEntity", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(ship, entity);
        foreach (var name in new[] { "_missionShipObject", "_actuators", "_physics", "<Formation>k__BackingField" })
        {
            var field = AccessTools.Field(typeof(MissionShip), name);
            field.SetValue(ship, FormatterServices.GetUninitializedObject(field.FieldType));
        }
        var physics = AccessTools.Field(typeof(MissionShip), "_physics").GetValue(ship);
        AccessTools.PropertySetter(physics.GetType(), "IsInitialized").Invoke(physics, new object[] { true });
        return ship;
    }

    private static bool Enable(WeakGameEntity gameEntity)
    {
        current.mutationBoundaries.Add("enable:" + gameEntity.Pointer + ":" + current.behavior.ActivationTransitions.Last().Phase);
        current.enables.Add(gameEntity.Pointer);
        if (current.enableSucceeds) current.activeBodies.Add(gameEntity.Pointer);
        return false;
    }

    private static bool Disable(WeakGameEntity gameEntity)
    {
        current.mutationBoundaries.Add("disable:" + gameEntity.Pointer + ":" + current.behavior.ActivationTransitions.Last().Phase);
        current.disables.Add(gameEntity.Pointer);
        current.activeBodies.Remove(gameEntity.Pointer);
        return false;
    }

    private static bool IsActive(WeakGameEntity gameEntity, ref bool __result)
    {
        __result = current.activeBodies.Contains(gameEntity.Pointer);
        return false;
    }

    private static bool SoundId(ref int __result) { __result = 0; return false; }

    private static bool HasDynamicBody(ref bool __result)
    {
        current.diagnosticReads++;
        if (current.dynamicBodyThrows) throw new InvalidOperationException("managed body query failure");
        __result = current.hasDynamicBody;
        return false;
    }

    private static bool PhysicsState(ref bool __result)
    {
        current.diagnosticReads++;
        __result = false;
        return false;
    }

    private static bool BodyFlag(ref BodyFlags __result)
    {
        current.diagnosticReads++;
        if (current.bodyFlagThrows) throw new InvalidOperationException("managed getter failure");
        __result = BodyFlags.None;
        return false;
    }

    [Fact]
    public void StartupDisable_SnapshotsSurroundEachMutationOnTheSameHull()
    {
        activeBodies.UnionWith(new[] { new UIntPtr(1), new UIntPtr(2) });
        behavior.DisableStartupBody(0);
        behavior.DisableStartupBody(1);
        var rows = behavior.ActivationTransitions;
        Assert.Equal(new[] { "startup_disable_before", "startup_disable_after", "startup_disable_before", "startup_disable_after" }, rows.Select(row => row.Phase));
        Assert.Equal(new ulong?[] { 1, 1, 2, 2 }, rows.Select(row => row.NativePointer));
        Assert.Equal(new bool?[] { true, false, true, false }, rows.Select(row => row.ActiveSimulation.Value));
        Assert.Equal(new[] { "disable:1:startup_disable_before", "disable:2:startup_disable_before" }, mutationBoundaries);
        Assert.Equal(2, disables.Count);
        Assert.Empty(enables);
    }

    [Fact]
    public void FailedConfirmation_CapturesBothHullsBeforeRollbackAndAfterEachDisable()
    {
        enableSucceeds = false;
        activeBodies.Add(new UIntPtr(2));
        behavior.FixedTicks = 17;
        behavior.SetAuthority(true);
        var rows = behavior.ActivationTransitions;
        Assert.Equal(new[] { "host_enable_before", "host_enable_after", "host_enable_before", "host_enable_after",
            "rollback_before", "rollback_before", "rollback_disable_after", "rollback_disable_after" }, rows.Select(row => row.Phase));
        Assert.Equal(new[] { 0, 0, 1, 1, 0, 1, 0, 1 }, rows.Select(row => row.Slot));
        Assert.Equal(new ulong?[] { 1, 1, 2, 2, 1, 2, 1, 2 }, rows.Select(row => row.NativePointer));
        Assert.Equal(Enumerable.Range(1, 8), rows.Select(row => row.Sequence));
        Assert.All(rows, row => { Assert.Equal(1, row.CallbackOrdinal); Assert.Equal(17, row.FixedTicks); });
        Assert.True(rows[5].ActiveSimulation.Value);
        Assert.False(rows[7].ActiveSimulation.Value);
        Assert.Equal(new[] { "enable:1:host_enable_before", "enable:2:host_enable_before",
            "disable:1:rollback_before", "disable:2:rollback_disable_after" }, mutationBoundaries);
        behavior.SetAuthority(true);
        Assert.Equal(8, behavior.ActivationTransitions.Length);
        Assert.Equal(2, enables.Count);
        Assert.Equal(2, disables.Count);
    }

    [Fact]
    public void TransitionRetention_StopsReadingAtCapacity()
    {
        for (int i = 0; i < NavalLabBehavior.ActivationTransitionLimit; i++)
            behavior.RecordActivationTransition(0, "test");
        int reads = diagnosticReads;
        for (int i = 0; i < 100; i++) behavior.RecordActivationTransition(0, "test");
        Assert.Equal(NavalLabBehavior.ActivationTransitionLimit, behavior.ActivationTransitions.Length);
        Assert.Equal(reads, diagnosticReads);
        var retained = behavior.ActivationTransitions;
        retained[0] = null!;
        Assert.NotNull(behavior.ActivationTransitions[0]);
        Assert.Empty(enables);
        Assert.Empty(disables);
    }

    [Fact]
    public void StoppedDiagnostics_RetainRowsWithoutFurtherNativeQueries()
    {
        behavior.RecordActivationTransition(0, "test");
        int reads = diagnosticReads;
        var adapter = new NavalMissionAdapter { behavior = behavior };
        adapter.Dispose();
        adapter.Dispose();
        for (int i = 0; i < 100; i++) behavior.RecordActivationTransition(0, "test");
        Assert.Single(behavior.ActivationTransitions);
        Assert.Equal(reads, diagnosticReads);
    }

    [Fact]
    public void MissingOrIncompleteHull_DoesNotQueryNativeBody()
    {
        behavior.Ships = new MissionShip[2];
        behavior.RecordActivationTransition(0, "test");
        Assert.Equal("ship_not_created", behavior.ActivationTransitions[0].Unavailable);
        Assert.Null(behavior.ActivationTransitions[0].NativePointer);
        behavior.Ships = new[] { Ship(0), Ship(2) };
        behavior.RecordActivationTransition(0, "test");
        Assert.Equal("invalid_ship_entity", behavior.ActivationTransitions[1].Unavailable);
        AccessTools.Field(typeof(MissionShip), "_actuators").SetValue(behavior.Ships[1], null);
        behavior.RecordActivationTransition(1, "test");
        Assert.Equal("incomplete_ship", behavior.ActivationTransitions[2].Unavailable);
        Assert.Equal(0, diagnosticReads);
    }

    [Fact]
    public void MissingBody_DoesNotReadDependentGettersOrFabricateFalse()
    {
        hasDynamicBody = false;
        behavior.RecordActivationTransition(0, "test");
        var row = Assert.Single(behavior.ActivationTransitions);
        Assert.False(row.DynamicBody.Value);
        Assert.Equal("dynamic_body_missing", row.Unavailable);
        Assert.Null(row.ActiveSimulation.Value);
        Assert.Null(row.BodyFlag.Value);
        Assert.Null(row.PhysicsState.Value);
        Assert.Equal(1, diagnosticReads);
    }

    [Fact]
    public void BodyQueryError_DoesNotReadDependentGettersOrBlockOriginalMutations()
    {
        dynamicBodyThrows = true;
        enableSucceeds = false;
        behavior.SetAuthority(true);
        Assert.Equal(2, enables.Count);
        Assert.Equal(2, disables.Count);
        Assert.Equal(8, diagnosticReads);
        Assert.All(behavior.ActivationTransitions, row =>
        {
            Assert.Equal("dynamic_body_read_error", row.Unavailable);
            Assert.Equal(typeof(InvalidOperationException).FullName, row.DynamicBody.Error);
            Assert.Null(row.DynamicBody.Value);
            Assert.Null(row.PhysicsState.Value);
        });
    }

    [Fact]
    public void TransitionSerialization_DistinguishesZeroFalseUnavailableAndReadError()
    {
        behavior.RecordActivationTransition(0, "test");
        bodyFlagThrows = true;
        behavior.RecordActivationTransition(1, "test");
        var rows = JArray.Parse(JsonConvert.SerializeObject(behavior.ActivationTransitions));
        Assert.Equal(0u, (uint)rows[0]["BodyFlag"]!["Value"]!);
        Assert.False((bool)rows[0]["PhysicsState"]!["Value"]!);
        Assert.Equal(JTokenType.Null, rows[0]["EngineBodySleeping"]!["Value"]!.Type);
        Assert.Equal("omitted_hull_getter_preconditions_unverified", (string?)rows[0]["EngineBodySleeping"]!["Unavailable"]);
        Assert.Equal(JTokenType.Null, rows[1]["BodyFlag"]!["Value"]!.Type);
        Assert.Equal(typeof(InvalidOperationException).FullName, (string?)rows[1]["BodyFlag"]!["Error"]);
        Assert.False((bool)rows[1]["PhysicsState"]!["Value"]!);
        Assert.DoesNotContain(rows.Descendants().OfType<JValue>(), value => value.Type == JTokenType.Float);
        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void SetAuthority_RequiresBothNativeBodiesActive_AndDoesNotReenableOnEveryTick()
    {
        behavior.SetAuthority(true);
        behavior.SetAuthority(true);
        Assert.True(behavior.Simulating);
        Assert.Null(behavior.Blocker);
        Assert.Equal(2, enables.Count);
        Assert.Empty(disables);
        Assert.Equal(new bool?[] { false, true, false, true }, behavior.ActivationTransitions.Select(row => row.ActiveSimulation.Value));
        Assert.All(behavior.ActivationTransitions, row => Assert.Equal(1, row.CallbackOrdinal));
    }

    [Fact]
    public void SetAuthority_UnconfirmedActivationHoldsAllBodies_AndCannotRetryThroughBlocker()
    {
        enableSucceeds = false;
        behavior.SetAuthority(true);
        Assert.False(behavior.Simulating);
        Assert.Equal("ship.activation_unconfirmed:slot_0", behavior.Blocker);
        Assert.Equal(2, disables.Count);
        behavior.SetAuthority(true);
        Assert.Equal(2, enables.Count);
    }

    [Fact]
    public void SetAuthority_PartiallyActivePairIsNotReady_AndActiveBodyIsHeld()
    {
        enableSucceeds = false;
        activeBodies.Add(new UIntPtr(1));
        behavior.SetAuthority(true);
        Assert.False(behavior.Simulating);
        Assert.Equal("ship.activation_unconfirmed:slot_1", behavior.Blocker);
        Assert.Empty(activeBodies);
    }

    [Fact]
    public void SetAuthority_LostNativeActivityDoesNotRemainLogicallySimulating()
    {
        behavior.SetAuthority(true);
        activeBodies.Remove(new UIntPtr(2));
        behavior.SetAuthority(true);
        Assert.False(behavior.Simulating);
        Assert.Equal("ship.activation_unconfirmed:slot_1", behavior.Blocker);
        Assert.Equal(2, enables.Count);
        Assert.Empty(activeBodies);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SetAuthority_IncompleteHullInventoryCannotActivate(bool missingSlot)
    {
        behavior.Ships = missingSlot ? new MissionShip[2] : Array.Empty<MissionShip>();
        behavior.SetAuthority(true);
        Assert.False(behavior.Simulating);
        Assert.Equal("ship.activation_unconfirmed:incomplete_hulls", behavior.Blocker);
        Assert.Empty(enables);
    }

    [Fact]
    public void SetAuthority_FollowerAndInitialHoldNeverEnableBodies()
    {
        behavior.SetAuthority(false);
        behavior.SetAuthority(false);
        Assert.False(behavior.Simulating);
        Assert.Null(behavior.Blocker);
        Assert.Empty(enables);
    }

    [Fact]
    public void SetAuthority_HoldOrStopDisablesConfirmedHost_AndIsIdempotent()
    {
        behavior.SetAuthority(true);
        behavior.SetAuthority(false);
        behavior.SetAuthority(false);
        Assert.False(behavior.Simulating);
        Assert.Empty(activeBodies);
        Assert.Equal(2, disables.Count);
    }

    [Fact]
    public void SetAuthority_FaultHoldsConfirmedHost_WithoutFurtherEnable()
    {
        behavior.SetAuthority(true);
        behavior.Reject("existing_fault");
        behavior.SetAuthority(true);
        Assert.False(behavior.Simulating);
        Assert.Empty(activeBodies);
        Assert.Equal("existing_fault", behavior.Blocker);
        Assert.Equal(2, enables.Count);
    }

    public void Dispose()
    {
        harmony.UnpatchAll(harmony.Id);
        current = null!;
    }
}
#endif
