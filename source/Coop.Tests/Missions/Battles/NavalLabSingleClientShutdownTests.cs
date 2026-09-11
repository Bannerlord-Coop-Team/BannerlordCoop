#if DEBUG
using HarmonyLib;
using Missions.Battles;
using Missions.Naval;
using Common.Tests.Utils;
using Coop.Tests.Mocks;
using GameInterface.Services.MapEvents;
using GameInterface.Services.Entity;
using GameInterface.Services.ObjectManager;
using Missions;
using Missions.Services.Network;
using Moq;
using NavalDLC.Missions.MissionLogics;
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
public sealed class NavalLabSingleClientShutdownTests : IDisposable
{
    private readonly Harmony harmony = new("coop.tests.naval.single.shutdown");
    private readonly MissionCurrentScope scope = new();
    private static NavalLabSingleClientShutdownTests current = null!;
    private NavalLabBehavior behavior;
    private MissionShip ship;
    private int bodyDisables;
    private int removalCalls;
    private bool retired;
    private bool failHold;
    private Exception? removalException;

    public NavalLabSingleClientShutdownTests()
    {
        current = this;
        NavalDLC.Missions.ShipActuators.SailWindProfile.InitializeProfile();
        harmony.Patch(AccessTools.Method(typeof(SoundEvent), nameof(SoundEvent.GetEventIdFromString)),
            prefix: new HarmonyMethod(typeof(NavalLabSingleClientShutdownTests), nameof(SoundId)));
        var id = Guid.NewGuid();
        behavior = new NavalLabBehavior(new NavalLabManifest("naval-lab:" + id.ToString("N"), id,
            new[] { "A" }, Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray(),
            new[] { Guid.NewGuid() }, NavalLabMode.SingleClientNative), "A", null!, null!);
        SetMission(behavior, scope.Instance);
        var managed = typeof(ScriptComponentBehavior).BaseType!.Assembly.GetType("TaleWorlds.DotNet.Managed")!;
        var moduleTypes = AccessTools.Field(managed, "_moduleTypes");
        var previous = moduleTypes.GetValue(null);
        try
        {
            if (previous == null) moduleTypes.SetValue(null, new Dictionary<string, Type>());
            ship = (MissionShip)FormatterServices.GetUninitializedObject(typeof(MissionShip));
        }
        finally { moduleTypes.SetValue(null, previous); }
        SetEntity(new UIntPtr(123));
        behavior.Ships = new[] { ship };
        NavalLabPhysicsPatches.Active = behavior;
        harmony.Patch(AccessTools.Method(typeof(GameEntityPhysicsExtensions), nameof(GameEntityPhysicsExtensions.DisableDynamicBodySimulation), new[] { typeof(WeakGameEntity) }),
            prefix: new HarmonyMethod(typeof(NavalLabSingleClientShutdownTests), nameof(DisableBody)));
        var method = AccessTools.Method(typeof(NavalShipsLogic), "OnEndMission");
        var patch = typeof(NavalLabPhysicsPatches).GetNestedType("SingleMissionEndHold", BindingFlags.NonPublic)!;
        harmony.Patch(method, prefix: new HarmonyMethod(AccessTools.Method(patch, "Prefix")) { priority = Priority.First });
        // Engine removal boundary: invalidate the same cached hull identity, not a still-usable mock hull.
        harmony.Patch(method, prefix: new HarmonyMethod(typeof(NavalLabSingleClientShutdownTests), nameof(RemoveNativeIdentities)) { priority = Priority.Last });
    }

    private static void SetMission(MissionBehavior target, Mission? mission) =>
        AccessTools.PropertySetter(typeof(MissionBehavior), nameof(MissionBehavior.Mission)).Invoke(target, new object?[] { mission });
    private void SetEntity(UIntPtr pointer)
    {
        var entity = Activator.CreateInstance(typeof(WeakGameEntity), BindingFlags.Instance | BindingFlags.NonPublic,
            null, new object[] { pointer }, null);
        AccessTools.Field(typeof(ScriptComponentBehavior), "_gameEntity").SetValue(ship, entity);
    }
    private static bool SoundId(ref int __result) { __result = 0; return false; }
    private static bool DisableBody(WeakGameEntity gameEntity)
    {
        Assert.False(current.retired);
        Assert.Equal(new UIntPtr(123), gameEntity.Pointer);
        current.bodyDisables++;
        if (current.failHold) throw new InvalidOperationException("native hold boundary failed");
        return false;
    }
    private static bool RemoveNativeIdentities()
    {
        current.removalCalls++;
        current.retired = true;
        current.SetEntity(UIntPtr.Zero);
        if (current.removalException != null) throw current.removalException;
        return false;
    }
    private void EndShips(Mission? mission)
    {
        var logic = new NavalShipsLogic();
        SetMission(logic, mission);
        AccessTools.Method(typeof(NavalShipsLogic), "OnEndMission").Invoke(logic, null);
    }

    [Fact]
    public void PreRemovalHold_RetiredHullIsNeverTouchedByLaterLeavingDisposeOrFinalize()
    {
        EndShips(scope.Instance);
        Assert.Equal(1, bodyDisables);
        Assert.Equal(1, removalCalls);
        Assert.False(ship.GameEntity.IsValid);
        Assert.True(behavior.nativeTerminalHold);
        var adapter = new NavalMissionAdapter { behavior = behavior };
        using var broker = new TestMessageBroker();
        var component = new Mock<ICoopMissionComponent> { DefaultValue = DefaultValue.Mock };
        using var controller = new NavalLabController(Mock.Of<IBattleNetwork>(), new TestNetwork(), broker,
            Mock.Of<IObjectManager>(), component.Object,
            Mock.Of<IControllerIdProvider>(value => value.ControllerId == "A"), Mock.Of<IBattleHostRegistry>(),
            Mock.Of<IMissionContext>(), new NavalLabMeasurement());
        AccessTools.Field(typeof(NavalLabController), "manifest").SetValue(controller, behavior.manifest);
        AccessTools.Field(typeof(NavalLabController), "adapter").SetValue(controller, adapter);
        AccessTools.Method(typeof(NavalLabController), "OnLeaving").Invoke(controller, null);
        controller.Dispose();
        behavior.OnMissionStateFinalized();
        adapter.Dispose();
        Assert.Equal(1, bodyDisables);
        Assert.Same(behavior, NavalLabPhysicsPatches.Active);
        Assert.True(behavior.RequiresProcessExit);
    }

    [Fact]
    public void PreRemovalHook_DoesNotHoldAnotherMission()
    {
        EndShips(null);
        Assert.Equal(0, bodyDisables);
        Assert.False(behavior.nativeTerminalHold);
        Assert.Equal(1, removalCalls);
    }

    [Theory]
    [InlineData(NavalLabMode.Activation)]
    [InlineData(NavalLabMode.HeldHelm)]
    public void PreRemovalHook_DoesNotChangeEitherTwoClientMode(NavalLabMode mode)
    {
        var id = Guid.NewGuid();
        behavior = new NavalLabBehavior(new NavalLabManifest("naval-lab:" + id.ToString("N"), id,
            new[] { "A", "B" }, Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray(),
            new[] { Guid.NewGuid(), Guid.NewGuid() }, mode), "A", null!, null!);
        SetMission(behavior, scope.Instance);
        behavior.Ships = new[] { ship };
        NavalLabPhysicsPatches.Active = behavior;
        EndShips(scope.Instance);
        Assert.Equal(0, bodyDisables);
        Assert.False(behavior.nativeTerminalHold);
        Assert.Equal(1, removalCalls);
    }

    [Fact]
    public void HoldFailure_DoesNotPreventOriginalRemovalOrReplaceItsException()
    {
        failHold = true;
        removalException = new InvalidOperationException("original removal failure");
        var thrown = Assert.Throws<TargetInvocationException>(() => EndShips(scope.Instance));
        Assert.Same(removalException, thrown.InnerException);
        Assert.Equal(1, removalCalls);
        Assert.StartsWith("shutdown.hold_failed:", behavior.Blocker);
        Assert.False(ship.GameEntity.IsValid);
    }

    [Fact]
    public void RemovalFailure_IsNotSwallowedOrReplacedAfterTheHold()
    {
        removalException = new InvalidOperationException("native removal boundary failed");
        var thrown = Assert.Throws<TargetInvocationException>(() => EndShips(scope.Instance));
        Assert.Same(removalException, thrown.InnerException);
        Assert.Equal(1, bodyDisables);
        Assert.False(ship.GameEntity.IsValid);
        behavior.Hold();
        Assert.Equal(1, bodyDisables);
    }

    public void Dispose()
    {
        NavalLabPhysicsPatches.Active = null;
        if (NavalDLC.Missions.ShipActuators.SailWindProfile.IsSailWindProfileInitialized)
            NavalDLC.Missions.ShipActuators.SailWindProfile.FinalizeProfile();
        harmony.UnpatchAll(harmony.Id);
        scope.Dispose();
    }
}
#endif
