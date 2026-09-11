#if DEBUG
using HarmonyLib;
using Missions.Battles;
using Missions.Naval;
using NavalDLC;
using NavalDLC.Missions.MissionLogics;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.Objects.UsableMachines;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public sealed class NavalLabHeldHelmTests : IDisposable
{
    private readonly Harmony harmony = new("coop.tests.naval.held-helm");
    private readonly MissionCurrentScope mission = new();
    private static NavalLabHeldHelmTests current = null!;
    private NavalLabBehavior behavior = null!;
    private Agent agent = null!;
    private StandingPoint point = null!;
    private ShipControllerMachine machine = null!;
    private int uses;
    private int placements;
    private int stops;
    private int deployments;
    private int originalTicks;
    private Agent? pilotAtOriginalTick;
    private bool reserved;
    private bool vacant;
    private bool disabled;
    private bool partialTakeThrows;
    private bool placementThrows;
    private int stopThrowsAt;
    private Agent? occupantDuringFailedTake;

    public NavalLabHeldHelmTests()
    {
        current = this;
        Patch(typeof(SoundEvent), nameof(SoundEvent.GetEventIdFromString), nameof(SoundId));
        Patch(typeof(MBAnimation), nameof(MBAnimation.GetActionCodeWithName), nameof(SoundId));
        Patch(typeof(Agent), "IsActive", nameof(True));
        PatchGetter(typeof(Agent), nameof(Agent.IsPlayerControlled), nameof(True));
        PatchGetter(typeof(Mission), nameof(Mission.MainAgent), nameof(MainAgent));
        PatchGetter(typeof(GameNetwork), nameof(GameNetwork.IsClientOrReplay), nameof(False));
        PatchGetter(typeof(UsableMissionObject), nameof(UsableMissionObject.HasAIMovingTo), nameof(Reserved));
        PatchGetter(typeof(UsableMissionObject), nameof(UsableMissionObject.IsDisabledForPlayers), nameof(Disabled));
        PatchGetter(typeof(UsableMissionObject), nameof(UsableMissionObject.MovingAgent), nameof(NoMovingAgent));
        Patch(typeof(ShipControllerMachine), "IsAttachedShipVacant", nameof(Vacant));
        Patch(typeof(Agent), "UseGameObject", nameof(Use));
        Patch(typeof(Agent), "StopUsingGameObject", nameof(Stop));
        Patch(typeof(ShipControllerMachine), "OnPilotAssignedDuringSpawn", nameof(Place));
        Patch(typeof(ShipControllerMachine), "OnDeploymentFinished", nameof(Deploy));
        var id = Guid.NewGuid();
        var manifest = new NavalLabManifest("naval-lab:" + id.ToString("N"), id, new[] { "A", "B" },
            Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray(), new[] { Guid.NewGuid(), Guid.NewGuid() }, NavalLabMode.HeldHelm);
        behavior = new NavalLabBehavior(manifest, "A", null!, null!);
        AccessTools.PropertySetter(typeof(MissionBehavior), nameof(MissionBehavior.Mission)).Invoke(behavior, new object[] { mission.Instance });
        var managed = typeof(ScriptComponentBehavior).BaseType!.Assembly.GetType("TaleWorlds.DotNet.Managed")!;
        var moduleTypes = AccessTools.Field(managed, "_moduleTypes");
        var previous = moduleTypes.GetValue(null);
        try
        {
            if (previous == null) moduleTypes.SetValue(null, new Dictionary<string, Type>());
            var ship = Shell<MissionShip>();
            machine = Shell<ShipControllerMachine>();
            point = Shell<StandingPoint>();
            var entity = Activator.CreateInstance(typeof(WeakGameEntity), BindingFlags.Instance | BindingFlags.NonPublic,
                null, new object[] { new UIntPtr(123) }, null);
            AccessTools.Field(typeof(ScriptComponentBehavior), "_gameEntity").SetValue(point, entity);
            Set(machine, nameof(UsableMachine.PilotStandingPoint), point);
            Set(machine, nameof(ShipControllerMachine.AttachedShip), ship);
            Set(ship, nameof(MissionShip.ShipControllerMachine), machine);
            agent = Shell<Agent>();
            AccessTools.Field(typeof(Agent), "_pointer").SetValue(agent, new UIntPtr(456));
            var formation = Shell<Formation>();
            Set(ship, nameof(MissionShip.Formation), formation);
            AccessTools.Field(typeof(Agent), "_formation").SetValue(agent, formation);
            behavior.Ships = new[] { ship, ship };
            behavior.Agents = new Agent[10];
            behavior.Agents[0] = agent;
            behavior.helmLifecycleInitialized[0] = true;
        }
        finally { moduleTypes.SetValue(null, previous); }
    }

    private static T Shell<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));
    private static void Set(object target, string property, object? value) =>
        AccessTools.Field(target.GetType(), property == nameof(UsableMissionObject.UserAgent)
            ? "_userAgent" : "<" + property + ">k__BackingField").SetValue(target, value);
    private void Patch(Type type, string method, string prefix) => harmony.Patch(AccessTools.Method(type, method),
        prefix: new HarmonyMethod(typeof(NavalLabHeldHelmTests), prefix));
    private void PatchGetter(Type type, string property, string prefix) => harmony.Patch(AccessTools.PropertyGetter(type, property),
        prefix: new HarmonyMethod(typeof(NavalLabHeldHelmTests), prefix));
    private static bool SoundId(ref int __result) { __result = 0; return false; }
    private static bool True(ref bool __result) { __result = true; return false; }
    private static bool False(ref bool __result) { __result = false; return false; }
    private static bool MainAgent(ref Agent __result) { __result = current.agent; return false; }
    private static bool Reserved(ref bool __result) { __result = current.reserved; return false; }
    private static bool Disabled(ref bool __result) { __result = current.disabled; return false; }
    private static bool Vacant(ref bool __result) { __result = current.vacant; return false; }
    private static bool NoMovingAgent(ref Agent? __result) { __result = null; return false; }
    private static bool Use(Agent __instance, UsableMissionObject usedObject)
    {
        current.uses++;
        Assert.Same(current.point, usedObject);
        Assert.Same(current.agent, __instance);
        Set(__instance, nameof(Agent.CurrentlyUsedGameObject), usedObject);
        if (current.partialTakeThrows)
        {
            Set(usedObject, nameof(UsableMissionObject.UserAgent), current.occupantDuringFailedTake);
            throw new InvalidOperationException("partial native use");
        }
        Set(usedObject, nameof(UsableMissionObject.UserAgent), __instance);
        current.behavior.OnObjectUsed(__instance, usedObject);
        return false;
    }
    private static bool OriginalTick(ShipControllerMachine __instance)
    {
        current.originalTicks++;
        current.pilotAtOriginalTick = __instance.PilotAgent;
        return false;
    }
    private void TickWithGuard()
    {
        NavalLabPhysicsPatches.Active = behavior;
        var patch = typeof(NavalLabPhysicsPatches).GetNestedType("HeldHelmTick", BindingFlags.NonPublic)!;
        var method = AccessTools.Method(typeof(ShipControllerMachine), "OnTick");
        harmony.Patch(method, prefix: new HarmonyMethod(AccessTools.Method(patch, "Prefix")) { priority = Priority.First });
        harmony.Patch(method, prefix: new HarmonyMethod(typeof(NavalLabHeldHelmTests), nameof(OriginalTick)) { priority = Priority.Last });
        method.Invoke(machine, new object[] { 0.1f });
    }
    private static bool Deploy() { current.deployments++; return false; }
    private static bool Place(ShipControllerMachine __instance)
    {
        Assert.Equal(1, current.uses - current.stops);
        Assert.Same(current.agent, __instance.PilotAgent);
        current.placements++;
        if (current.placementThrows) throw new InvalidOperationException("native placement failed");
        return false;
    }
    private static bool Stop(Agent __instance)
    {
        current.stops++;
        Assert.True(ReferenceEquals(current.point.UserAgent, null) || ReferenceEquals(current.point.UserAgent, current.agent));
        if (current.stopThrowsAt == 1) throw new InvalidOperationException("before cleanup");
        Set(current.point, nameof(UsableMissionObject.UserAgent), null);
        if (current.stopThrowsAt == 2) throw new InvalidOperationException("after point cleanup");
        Set(__instance, nameof(Agent.CurrentlyUsedGameObject), null);
        if (current.stopThrowsAt == 3) throw new InvalidOperationException("after both identities cleared");
        current.behavior.OnObjectStoppedBeingUsed(__instance, current.point);
        return false;
    }

    [Fact]
    public void TakeRelease_UsesExactNativePairOncePerOccupationAndAllowsRetake()
    {
        Assert.Equal("taken", behavior.SetHeldHelm(0, true));
        Assert.Equal("already_taken", behavior.SetHeldHelm(0, true));
        Assert.Equal(1, uses);
        Assert.Equal(1, placements);
        Assert.Equal("released", behavior.SetHeldHelm(0, false));
        Assert.Equal("already_released", behavior.SetHeldHelm(0, false));
        Assert.Equal(1, stops);
        Assert.Equal("taken", behavior.SetHeldHelm(0, true));
        Assert.Equal(2, uses);
        behavior.CancelControls();
        behavior.CancelControls();
        Assert.Equal(2, stops);
        Assert.Equal(2, behavior.helmUseCallbacks);
        Assert.Equal(2, behavior.helmStopCallbacks);
    }

    [Fact]
    public void InvalidOwnerMissingPointOrLifecycle_DoesNotConsumeUse()
    {
        Assert.Equal("rejected:not_original_owner_or_unavailable", behavior.SetHeldHelm(1, true));
        behavior.helmLifecycleInitialized[0] = false;
        Assert.Equal("rejected:helm_lifecycle_unavailable", behavior.SetHeldHelm(0, true));
        behavior.helmLifecycleInitialized[0] = true;
        Set(machine, nameof(UsableMachine.PilotStandingPoint), null);
        Assert.Equal("rejected:standing_point_unavailable", behavior.SetHeldHelm(0, true));
        Set(machine, nameof(UsableMachine.PilotStandingPoint), point);
        Assert.Equal("taken", behavior.SetHeldHelm(0, true));
        Assert.Equal(1, uses);
    }

    [Fact]
    public void ForeignOccupantReservationVacancyAndOtherUse_AreNeverDisplaced()
    {
        var foreign = Shell<Agent>();
        Set(point, nameof(UsableMissionObject.UserAgent), foreign);
        Assert.Equal("rejected:foreign_occupant", behavior.SetHeldHelm(0, true));
        Assert.Equal("rejected:foreign_occupant", behavior.SetHeldHelm(0, false));
        Assert.Same(foreign, point.UserAgent);
        Assert.Null(agent.CurrentlyUsedGameObject);
        Assert.Null(behavior.heldHelmAgent);
        Set(point, nameof(UsableMissionObject.UserAgent), null);
        reserved = true;
        Assert.Equal("rejected:standing_point_reserved", behavior.SetHeldHelm(0, true));
        Assert.True(reserved);
        Assert.Null(point.UserAgent);
        Assert.Null(agent.CurrentlyUsedGameObject);
        Assert.Null(behavior.heldHelmAgent);
        reserved = false;
        disabled = true;
        Assert.Equal("rejected:helm_disabled_or_ship_vacant", behavior.SetHeldHelm(0, true));
        Assert.True(disabled);
        Assert.Null(point.UserAgent);
        Assert.Null(agent.CurrentlyUsedGameObject);
        disabled = false;
        vacant = true;
        Assert.Equal("rejected:helm_disabled_or_ship_vacant", behavior.SetHeldHelm(0, true));
        Assert.True(vacant);
        Assert.Null(point.UserAgent);
        Assert.Null(agent.CurrentlyUsedGameObject);
        Assert.Null(behavior.heldHelmAgent);
        vacant = false;
        Set(agent, nameof(Agent.CurrentlyUsedGameObject), Shell<StandingPoint>());
        Assert.Equal("rejected:agent_using_other_object", behavior.SetHeldHelm(0, true));
        Assert.Equal(0, uses);
        Assert.Equal(0, stops);
    }

    [Fact]
    public void ExpiryAndStop_ReleaseOnlyTheOwnedOccupation()
    {
        behavior.SetHeldHelm(0, true);
        behavior.heldHelmDeadline = 0;
        behavior.TickAgentControl(0);
        Assert.Equal(1, stops);
        Assert.Null(behavior.heldHelmAgent);
        behavior.SetHeldHelm(0, true);
        var foreign = Shell<Agent>();
        Set(point, nameof(UsableMissionObject.UserAgent), foreign);
        behavior.CancelControls();
        Assert.Same(foreign, point.UserAgent);
        Assert.Equal(1, stops);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FailedTake_CleansOnlyExactOwnedPartialOrCompletedUse(bool partial)
    {
        partialTakeThrows = partial;
        placementThrows = !partial;
        Assert.StartsWith("failed:", behavior.SetHeldHelm(0, true));
        Assert.Null(point.UserAgent);
        Assert.Null(agent.CurrentlyUsedGameObject);
        Assert.Equal(1, stops);
        Assert.Null(behavior.heldHelmAgent);
        Assert.StartsWith("helm.take_failed:", behavior.Blocker);
        behavior.CancelControls();
        Assert.Equal(1, stops);
    }

    [Fact]
    public void NativeStoppedUse_CanBeRetakenWithoutReusingAnOldLease()
    {
        behavior.SetHeldHelm(0, true);
        AccessTools.Method(typeof(Agent), "StopUsingGameObject").Invoke(agent, new object[] { true, Agent.StopUsingGameObjectFlags.AutoAttachAfterStoppingUsingGameObject });
        TickWithGuard();
        Assert.Null(behavior.Blocker);
        Assert.Null(behavior.heldHelmAgent);
        Assert.Equal(0, behavior.heldHelmDeadline);
        Assert.Equal("taken", behavior.SetHeldHelm(0, true));
        Assert.Equal(2, uses);
        Assert.Equal(2, placements);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void FailedStop_RetainsLeaseUntilBothNativeIdentitiesConfirmRelease(int failure)
    {
        behavior.SetHeldHelm(0, true);
        double deadline = behavior.heldHelmDeadline;
        stopThrowsAt = failure;
        Assert.StartsWith("failed:", behavior.SetHeldHelm(0, false));
        Assert.StartsWith("helm.release_failed:", behavior.Blocker);
        if (failure < 3)
        {
            Assert.Same(agent, behavior.heldHelmAgent);
            Assert.Same(point, behavior.heldHelmPoint);
            Assert.Equal(deadline, behavior.heldHelmDeadline);
        }
        else Assert.Null(behavior.heldHelmAgent);
        stopThrowsAt = 0;
        behavior.CancelControls();
        Assert.Null(point.UserAgent);
        Assert.Null(agent.CurrentlyUsedGameObject);
        Assert.Null(behavior.heldHelmAgent);
        Assert.Equal(failure < 3 ? 2 : 1, stops);
    }

    [Fact]
    public void FailedTake_DoesNotEvictAnOccupantThatChangedDuringTheCall()
    {
        partialTakeThrows = true;
        occupantDuringFailedTake = Shell<Agent>();
        Assert.StartsWith("failed:", behavior.SetHeldHelm(0, true));
        behavior.CancelControls();
        Assert.Same(occupantDuringFailedTake, point.UserAgent);
        Assert.Same(point, agent.CurrentlyUsedGameObject);
        Assert.Equal(0, stops);
    }

    [Fact]
    public void HelmLifecycle_InitializesOnlyOwnedMachineExactlyOnce()
    {
        behavior.helmLifecycleInitialized[0] = false;
        behavior.InitializeHelmLifecycle(1);
        Assert.Equal(0, deployments);
        behavior.InitializeHelmLifecycle(0);
        behavior.InitializeHelmLifecycle(0);
        behavior.SetHeldHelm(0, true);
        behavior.SetHeldHelm(0, true);
        Assert.Equal(1, deployments);
        Assert.True(behavior.helmLifecycleInitialized[0]);
        Assert.False(behavior.helmLifecycleInitialized[1]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void VacancyOrDisableAfterTake_ReleasesBeforeOriginalTickAndCannotEnterPilotCaptureBranch(bool disable)
    {
        behavior.SetHeldHelm(0, true);
        if (disable) disabled = true;
        else vacant = true;
        TickWithGuard();
        Assert.Equal(1, originalTicks);
        Assert.Null(pilotAtOriginalTick);
        Assert.Null(point.UserAgent);
        Assert.Null(agent.CurrentlyUsedGameObject);
        Assert.Equal(1, stops);
        Assert.Equal(1, behavior.helmStopCallbacks);
        behavior.BeforeHeldHelmTick(machine);
        behavior.CancelControls();
        Assert.Equal(1, stops);
        Assert.Null(behavior.Blocker);
    }

    [Fact]
    public void ChangedOccupantBeforeTick_IsNotReleasedAndFaultsFixture()
    {
        behavior.SetHeldHelm(0, true);
        var foreign = Shell<Agent>();
        Set(point, nameof(UsableMissionObject.UserAgent), foreign);
        vacant = true;
        TickWithGuard();
        Assert.Equal(1, originalTicks);
        Assert.Same(foreign, pilotAtOriginalTick);
        Assert.Same(foreign, point.UserAgent);
        Assert.Equal(0, stops);
        Assert.Equal("helm.use_identity_changed_before_tick", behavior.Blocker);
        behavior.CancelControls();
        Assert.Same(foreign, point.UserAgent);
        Assert.Equal(0, stops);
    }

    [Fact]
    public void ReplacedStandingPointBeforeTick_IsNotReleasedAndFaultsFixture()
    {
        behavior.SetHeldHelm(0, true);
        var replacement = Shell<StandingPoint>();
        var foreign = Shell<Agent>();
        Set(replacement, nameof(UsableMissionObject.UserAgent), foreign);
        Set(machine, nameof(UsableMachine.PilotStandingPoint), replacement);
        Set(agent, nameof(Agent.CurrentlyUsedGameObject), replacement);
        vacant = true;
        TickWithGuard();
        Assert.Equal(1, originalTicks);
        Assert.Same(foreign, pilotAtOriginalTick);
        Assert.Equal("helm.use_identity_changed_before_tick", behavior.Blocker);
        behavior.CancelControls();
        Assert.Same(foreign, replacement.UserAgent);
        Assert.Same(replacement, agent.CurrentlyUsedGameObject);
        Assert.Equal(0, stops);
    }

    [Fact]
    public void NormalModeAndOtherHelms_AreNotChangedByPreTickGuard()
    {
        behavior.SetHeldHelm(0, true);
        vacant = true;
        behavior.BeforeHeldHelmTick(Shell<ShipControllerMachine>());
        Assert.Equal(0, stops);
        var old = behavior.manifest;
        var normal = new NavalLabManifest(old.InstanceId, old.IncarnationId, old.Controllers, old.Combatants, old.Ships);
        behavior = new NavalLabBehavior(normal, "A", null!, null!)
        {
            Ships = behavior.Ships, Agents = behavior.Agents,
            heldHelmAgent = agent, heldHelmPoint = point
        };
        TickWithGuard();
        Assert.Equal(1, originalTicks);
        Assert.Same(agent, pilotAtOriginalTick);
        Assert.Equal(0, stops);
    }

    [Fact]
    public void HeldMode_DoesNotTryActivationEvenWhenRequestedDirectly()
    {
        behavior.SetAuthority(true);
        Assert.False(behavior.Simulating);
        Assert.Null(behavior.Blocker);
        Assert.Empty(behavior.ActivationTransitions);
    }

    private int baseTicks;
    private int captureLookups;
    private int captureAnimations;
    private int rudderAnimations;
    private int captureDelegations;
    private readonly List<MissionShip> invalidatedShips = new();
    private bool vacancyDuringBaseTick;
    private bool captureSideEffectThrows;
    private ShipAssignment assignment = null!;
    private MissionShip previousShip = null!;

    private static bool BaseTick()
    {
        current.baseTicks++;
        if (current.vacancyDuringBaseTick) current.vacant = true;
        return false;
    }
    private static bool NoMain(ref Agent? __result) { __result = null; return false; }
    private static bool Assignment(ref ShipAssignment __result)
    {
        current.captureLookups++;
        if (current.captureSideEffectThrows) throw new InvalidOperationException("capture lookup callback failure");
        __result = current.assignment;
        return false;
    }
    private static bool Action(int channelNo, ref bool __result)
    {
        if (channelNo == 0) current.captureAnimations++;
        else current.rudderAnimations++;
        __result = true;
        return false;
    }
    private static bool Capture()
    {
        current.captureDelegations++;
        return false;
    }
    private static bool Invalidate(MissionShip __instance)
    {
        current.invalidatedShips.Add(__instance);
        return false;
    }
    private static bool ZeroFloat(ref float __result) { __result = 0; return false; }
    private static bool Scale(ref Vec3 __result) { __result = Vec3.One; return false; }
    private static bool ActionSet(ref MBActionSet __result) { __result = default; return false; }
    private static bool Animation(ref ActionIndexCache __result) { __result = default; return false; }

    private void InstallNativeTick(bool guard = true, bool adapterOwned = false)
    {
        NavalLabPhysicsPatches.Active = behavior;
        Patch(typeof(UsableMachine), "OnTick", nameof(BaseTick));
        PatchGetter(typeof(Agent), nameof(Agent.Main), nameof(NoMain));
        PatchGetter(typeof(Agent), nameof(Agent.IsMainAgent), nameof(True));
        Patch(typeof(NavalShipsLogic), "GetShipAssignment", nameof(Assignment));
        Patch(typeof(MissionShip), "AreShipsConnected", nameof(True));
        Patch(typeof(Agent), "SetActionChannel", nameof(Action));
        Patch(typeof(NavalShipsLogic), nameof(NavalShipsLogic.OnShipCaptured), nameof(Capture));
        Patch(typeof(MissionShip), "InvalidateActiveFormationTroopOnShipCache", nameof(Invalidate));
        PatchGetter(typeof(MissionShip), nameof(MissionShip.VisualRudderRotationPercentage), nameof(ZeroFloat));
        PatchGetter(typeof(MissionShip), nameof(MissionShip.VisualRudderPullDirection), nameof(ZeroFloat));
        Patch(typeof(WeakGameEntity), nameof(WeakGameEntity.GetGlobalScale), nameof(Scale));
        PatchGetter(typeof(Agent), nameof(Agent.ActionSet), nameof(ActionSet));
        Patch(typeof(MBActionSet), nameof(MBActionSet.GetAnimationIndexOfAction), nameof(SoundId));
        harmony.Patch(AccessTools.Method(typeof(MBAnimation), nameof(MBAnimation.GetAnimationBlendsWithActionIndex), new[] { typeof(int) }),
            prefix: new HarmonyMethod(typeof(NavalLabHeldHelmTests), nameof(Animation)));
        AccessTools.Field(typeof(ShipControllerMachine), "_navalShipsLogic").SetValue(machine, Shell<NavalShipsLogic>());
        AccessTools.Field(typeof(ShipControllerMachine), "_captureTimer").SetValue(machine, 0.05f);
        AccessTools.Field(typeof(Formation), nameof(Formation.Team)).SetValue(agent.Formation, Shell<Team>());
        assignment = Shell<ShipAssignment>();
        previousShip = Shell<MissionShip>();
        Set(assignment, nameof(ShipAssignment.MissionShip), previousShip);
        var method = AccessTools.Method(typeof(ShipControllerMachine), "OnTick");
        var patch = typeof(NavalLabPhysicsPatches).GetNestedType("HeldCaptureBranch", BindingFlags.NonPublic)!;
        var tickHarmony = adapterOwned ? new Harmony("coop.warsails.lab.physics") : harmony;
        tickHarmony.Patch(method, transpiler: new HarmonyMethod(AccessTools.Method(patch, "Transpiler")));
        if (guard)
        {
            var prefix = typeof(NavalLabPhysicsPatches).GetNestedType("HeldHelmTick", BindingFlags.NonPublic)!;
            tickHarmony.Patch(method, prefix: new HarmonyMethod(AccessTools.Method(prefix, "Prefix")));
        }
    }
    private void NativeTick() => AccessTools.Method(typeof(ShipControllerMachine), "OnTick").Invoke(machine, new object[] { 0.1f });
    private void AssertNoCaptureSideEffects()
    {
        Assert.Equal(0, captureLookups);
        Assert.Equal(0, captureAnimations);
        Assert.Equal(0, captureDelegations);
        Assert.Empty(invalidatedShips);
        Assert.Equal(0, rudderAnimations);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void CaptureBarrier_RealNativeTickSurvivesEachCleanupFailureWithoutCaptureSideEffects(int failure)
    {
        behavior.SetHeldHelm(0, true);
        stopThrowsAt = failure;
        vacant = true;
        InstallNativeTick();
        NativeTick();
        Assert.Equal(1, baseTicks);
        Assert.Equal(1, stops);
        AssertNoCaptureSideEffects();
        Assert.StartsWith("helm.release_failed:", behavior.Blocker);
        if (failure < 3) Assert.Same(agent, behavior.heldHelmAgent);
        else Assert.Null(behavior.heldHelmAgent);
        NativeTick();
        Assert.Equal(2, baseTicks);
        AssertNoCaptureSideEffects();
    }

    [Fact]
    public void CaptureBarrier_VacancyArisingInsideNativeTickCannotReachThrowingCaptureLookup()
    {
        behavior.SetHeldHelm(0, true);
        vacancyDuringBaseTick = true;
        captureSideEffectThrows = true;
        stopThrowsAt = 1;
        InstallNativeTick();
        NativeTick();
        Assert.Equal(1, baseTicks);
        Assert.Equal(0, stops);
        AssertNoCaptureSideEffects();
        Assert.Equal("helm.capture_suppressed", behavior.Blocker);
        Assert.Same(agent, point.UserAgent);
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("normal")]
    [InlineData("other-machine")]
    [InlineData("other-attachment")]
    [InlineData("other-owner")]
    public void CaptureBarrier_NonfixtureAndNormalNativeCaptureStillExecute(string scope)
    {
        behavior.SetHeldHelm(0, true);
        InstallNativeTick(false);
        vacant = true;
        if (scope == "inactive") NavalLabPhysicsPatches.Active = null;
        if (scope == "normal")
        {
            var old = behavior.manifest;
            var normal = new NavalLabManifest(old.InstanceId, old.IncarnationId, old.Controllers, old.Combatants, old.Ships);
            NavalLabPhysicsPatches.Active = new NavalLabBehavior(normal, "A", null!, null!) { Ships = behavior.Ships };
        }
        if (scope == "other-machine") Set(behavior.Ships[0], nameof(MissionShip.ShipControllerMachine), Shell<ShipControllerMachine>());
        if (scope == "other-attachment") Set(machine, nameof(ShipControllerMachine.AttachedShip), previousShip);
        if (scope == "other-owner")
            NavalLabPhysicsPatches.Active = new NavalLabBehavior(behavior.manifest, "B", null!, null!) { Ships = new[] { behavior.Ships[0], previousShip } };
        NativeTick();
        Assert.Equal(1, baseTicks);
        Assert.Equal(1, captureLookups);
        Assert.Equal(1, captureAnimations);
        Assert.Equal(1, stops);
        Assert.Equal(1, captureDelegations);
        Assert.Equal(new[] { previousShip, machine.AttachedShip }, invalidatedShips);
        Assert.Equal(0, rudderAnimations);
    }

    [Fact]
    public void CaptureBarrier_NonfixtureCaptureLookupExceptionIsNotSwallowed()
    {
        behavior.SetHeldHelm(0, true);
        InstallNativeTick(false);
        NavalLabPhysicsPatches.Active = null;
        vacant = true;
        captureSideEffectThrows = true;
        Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(NativeTick).InnerException);
        Assert.Equal(1, captureLookups);
    }

    [Fact]
    public void CaptureBarrier_NoncaptureRudderAnimationAndBaseTickRemainOriginal()

    {
        behavior.SetHeldHelm(0, true);
        InstallNativeTick();
        NativeTick();
        Assert.Equal(1, baseTicks);
        Assert.Equal(1, rudderAnimations);
        Assert.Equal(0, captureLookups);
        Assert.Equal(0, captureAnimations);
        Assert.Equal(0, stops);
        Assert.Null(behavior.Blocker);
    }

    [Fact]
    public void CaptureBarrier_UnsupportedShapeFailsWithoutReturningModifiedInstructions()
    {
        var patch = typeof(NavalLabPhysicsPatches).GetNestedType("HeldCaptureBranch", BindingFlags.NonPublic)!;
        var code = new[] { new CodeInstruction(OpCodes.Ret) };
        var generator = new DynamicMethod("unsupported", typeof(void), Type.EmptyTypes).GetILGenerator();
        var failure = Assert.Throws<TargetInvocationException>(() => AccessTools.Method(patch, "Transpiler").Invoke(null, new object[] { code, generator }));
        Assert.IsType<InvalidOperationException>(failure.InnerException);
        Assert.Empty(code[0].labels);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CaptureBarrier_HeldDisposeAndBehaviorRemovalRetainExactScopeUntilProcessExit(bool stopFails)
    {
        behavior.SetHeldHelm(0, true);
        InstallNativeTick(adapterOwned: true);
        var adapter = new NavalMissionAdapter { behavior = behavior };
        stopThrowsAt = stopFails ? 1 : 0;
        adapter.Dispose();
        adapter.Dispose();
        Assert.Equal(1, stops);
        Assert.Null(adapter.behavior);
        Assert.Same(behavior, NavalLabPhysicsPatches.Active);
        // Native RemoveMissionBehavior clears Mission; identity must not depend on it or native entity pointers.
        AccessTools.PropertySetter(typeof(MissionBehavior), nameof(MissionBehavior.Mission)).Invoke(behavior, new object?[] { null });
        Assert.Equal(UIntPtr.Zero, ((WeakGameEntity)AccessTools.Field(typeof(ScriptComponentBehavior), "_gameEntity").GetValue(machine)).Pointer);
        Assert.Equal(UIntPtr.Zero, ((WeakGameEntity)AccessTools.Field(typeof(ScriptComponentBehavior), "_gameEntity").GetValue(behavior.Ships[0])).Pointer);
        Assert.True(behavior.SuppressHeldCapture(machine));
        Assert.False(behavior.SuppressHeldCapture(Shell<ShipControllerMachine>()));
        Set(machine, nameof(ShipControllerMachine.AttachedShip), previousShip);
        Assert.False(behavior.SuppressHeldCapture(machine));
        Set(machine, nameof(ShipControllerMachine.AttachedShip), behavior.Ships[0]);
        Assert.True(behavior.SuppressHeldCapture(machine));
        Assert.Contains("coop.warsails.lab.physics", Harmony.GetPatchInfo(AccessTools.Method(typeof(ShipControllerMachine), "OnTick")).Owners);
        var next = new NavalMissionAdapter();
        Assert.Contains("process exit", Assert.Throws<InvalidOperationException>(() => next.Open(behavior.manifest, null!, "A")).Message);
        // Disposing an unused adapter must not remove the retained held guard either.
        next.Dispose();
        Assert.Same(behavior, NavalLabPhysicsPatches.Active);
        vacant = true;
        NativeTick();
        AssertNoCaptureSideEffects();
        if (stopFails) Assert.Same(agent, behavior.heldHelmAgent);
        else Assert.Null(behavior.heldHelmAgent);
    }

    [Fact]
    public void CaptureBarrier_UnboundHeldOpenFailureStillRequiresProcessExit()
    {
        behavior = new NavalLabBehavior(behavior.manifest, "A", null!, null!);
        var adapter = new NavalMissionAdapter { behavior = behavior };
        NavalLabPhysicsPatches.Active = behavior;
        adapter.Dispose();
        adapter.Dispose();
        Assert.Same(behavior, NavalLabPhysicsPatches.Active);
        Assert.Null(adapter.behavior);
        Assert.Contains("process exit", Assert.Throws<InvalidOperationException>(new NavalMissionAdapter().Preflight).Message);
    }

    [Theory]
    [InlineData("callback")]
    [InlineData("exception-block")]
    [InlineData("entry-bypass")]
    [InlineData("condition")]
    public void CaptureBarrier_ChangedRealNativeShapeFailsClosed(string mutation)
    {
        var method = AccessTools.Method(typeof(ShipControllerMachine), "OnTick");
        var code = PatchProcessor.GetOriginalInstructions(method, out var generator);
        int capture = code.FindIndex(instruction => instruction.Calls(AccessTools.Method(typeof(ShipControllerMachine), "OnShipCapturedByAgent")));
        if (mutation == "callback") code[capture + 2] = new CodeInstruction(OpCodes.Nop);
        if (mutation == "exception-block") code[capture].blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
        if (mutation == "entry-bypass")
        {
            var label = generator.DefineLabel();
            code[capture - 2].labels.Add(label);
            code.Insert(0, new CodeInstruction(OpCodes.Br, label));
        }
        if (mutation == "condition")
        {
            int main = code.FindIndex(instruction => instruction.Calls(AccessTools.PropertyGetter(typeof(Agent), nameof(Agent.IsMainAgent))));
            code[main + 1].opcode = OpCodes.Brtrue;
        }
        var patch = typeof(NavalLabPhysicsPatches).GetNestedType("HeldCaptureBranch", BindingFlags.NonPublic)!;
        var failure = Assert.Throws<TargetInvocationException>(() => AccessTools.Method(patch, "Transpiler").Invoke(null, new object[] { code, generator }));
        Assert.IsType<InvalidOperationException>(failure.InnerException);
    }

    [Fact]
    public void CaptureBarrier_UntrackedPilotStillCannotCaptureOwnedHeldFixture()
    {
        behavior.SetHeldHelm(0, true);
        behavior.heldHelmAgent = null;
        behavior.heldHelmPoint = null;
        vacant = true;
        InstallNativeTick();
        NativeTick();
        AssertNoCaptureSideEffects();
        Assert.Equal(0, stops);
        Assert.Equal("helm.capture_suppressed", behavior.Blocker);
    }

    [Fact]
    public void NativePartialStop_StillFaultsOnInterveningGuardTick()
    {
        behavior.SetHeldHelm(0, true);
        Set(point, nameof(UsableMissionObject.UserAgent), null);
        TickWithGuard();
        Assert.Equal("helm.use_identity_changed_before_tick", behavior.Blocker);
        Assert.Same(agent, behavior.heldHelmAgent);
        Assert.Equal(0, stops);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NormalActivation_DisposeImmediatelyClearsActiveAndUnpatchesWithOrWithoutMission(bool bound)
    {
        var old = behavior.manifest;
        behavior = new NavalLabBehavior(new NavalLabManifest(old.InstanceId, old.IncarnationId, old.Controllers, old.Combatants, old.Ships), "A", null!, null!);
        if (bound) AccessTools.PropertySetter(typeof(MissionBehavior), nameof(MissionBehavior.Mission)).Invoke(behavior, new object[] { mission.Instance });
        InstallNativeTick(adapterOwned: true);
        var adapter = new NavalMissionAdapter { behavior = behavior };
        Assert.False(behavior.SuppressHeldCapture(machine));
        adapter.Dispose();
        adapter.Dispose();
        Assert.Null(NavalLabPhysicsPatches.Active);
        Assert.Null(adapter.behavior);
        Assert.DoesNotContain("coop.warsails.lab.physics", Harmony.GetPatchInfo(AccessTools.Method(typeof(ShipControllerMachine), "OnTick"))?.Owners.AsEnumerable() ?? Array.Empty<string>());
    }

    private static bool PlayerController(ref AgentControllerType __result) { __result = AgentControllerType.Player; return false; }
    private static bool ActiveState(ref AgentState __result) { __result = AgentState.Active; return false; }
    private static bool NoWeapon(ref TaleWorlds.Core.EquipmentIndex __result) { __result = TaleWorlds.Core.EquipmentIndex.None; return false; }
    private static bool NoEngineCall() => false;
    private static bool NativePlacement() { current.placements++; return false; }

    private void NativeUse() => AccessTools.Method(typeof(Agent), "UseGameObject").Invoke(agent, new object[] { point, -1 });
    private void NativeStop() => AccessTools.Method(typeof(Agent), "StopUsingGameObject").Invoke(agent,
        new object[] { true, Agent.StopUsingGameObjectFlags.AutoAttachAfterStoppingUsingGameObject });

    private void InstallNativeUseTrace()
    {
        harmony.Unpatch(AccessTools.Method(typeof(Agent), "UseGameObject"), HarmonyPatchType.Prefix, harmony.Id);
        harmony.Unpatch(AccessTools.Method(typeof(Agent), "StopUsingGameObject"), HarmonyPatchType.Prefix, harmony.Id);
        harmony.Unpatch(AccessTools.Method(typeof(ShipControllerMachine), "OnPilotAssignedDuringSpawn"), HarmonyPatchType.Prefix, harmony.Id);
        Patch(typeof(ShipControllerMachine), "OnPilotAssignedDuringSpawn", nameof(NativePlacement));
        PatchGetter(typeof(Agent), nameof(Agent.Controller), nameof(PlayerController));
        PatchGetter(typeof(Agent), nameof(Agent.State), nameof(ActiveState));
        harmony.Unpatch(AccessTools.Method(typeof(Agent), "IsActive"), HarmonyPatchType.Prefix, harmony.Id);
        harmony.Unpatch(AccessTools.PropertyGetter(typeof(Agent), nameof(Agent.IsPlayerControlled)), HarmonyPatchType.Prefix, harmony.Id);
        PatchGetter(typeof(GameNetwork), nameof(GameNetwork.IsServerOrRecorder), nameof(False));
        Patch(typeof(Agent), "IsInWater", nameof(True));
        Patch(typeof(Agent), "GetPrimaryWieldedItemIndex", nameof(NoWeapon));
        Patch(typeof(Agent), "GetOffhandWieldedItemIndex", nameof(NoWeapon));
        Patch(typeof(ScriptComponentBehavior), "SetScriptComponentToTickMT", nameof(NoEngineCall));
        Set(agent, nameof(Agent.Mission), mission.Instance);
        Set(mission.Instance, nameof(Mission.MissionBehaviors), new List<MissionBehavior> { behavior });
        AccessTools.Field(typeof(Agent), "_components").SetValue(agent, new MBList<AgentComponent>());
        AccessTools.Field(typeof(UsableMissionObject), "_components").SetValue(point, new List<UsableMissionObjectComponent>());
        NavalLabPhysicsPatches.Active = behavior;
        foreach (var pair in new[] { ("HeldUseTrace", "UseGameObject"), ("HeldStopTrace", "StopUsingGameObjectAux") })
        {
            var patch = typeof(NavalLabPhysicsPatches).GetNestedType(pair.Item1, BindingFlags.NonPublic)!;
            harmony.Patch(AccessTools.Method(typeof(Agent), pair.Item2),
                prefix: new HarmonyMethod(AccessTools.Method(patch, "Prefix")),
                finalizer: new HarmonyMethod(AccessTools.Method(patch, "Finalizer")));
        }
    }

    [Fact]
    public void Trace_RealStopRetirementThenRealReuseRemainsAttributedWithoutClaimingOwnership()
    {
        InstallNativeUseTrace();
        Assert.Equal("taken", behavior.SetHeldHelm(0, true));
        NativeStop();
        Assert.Null(point.UserAgent);
        Assert.Null(agent.CurrentlyUsedGameObject);
        behavior.BeforeHeldHelmTick(machine);
        Assert.Null(behavior.heldHelmAgent);
        NativeUse();
        Assert.Same(agent, point.UserAgent);
        Assert.Same(point, agent.CurrentlyUsedGameObject);
        Assert.Equal("rejected:unowned_use", behavior.SetHeldHelm(0, false));
        Assert.Equal(1, behavior.helmUseCallbacks);
        Assert.Equal(1, behavior.helmStopCallbacks);
        Assert.Equal(0, behavior.helmReleaseCalls);
        var trace = behavior.HelmTrace;
        Assert.Equal(new[] { "use:entry", "use:callback", "use:exit", "stop:entry", "stop:callback", "stop:exit",
            "retirement:before", "retirement:after", "use:entry", "use:callback", "use:exit" }, trace.Select(row => row.Phase));
        Assert.All(trace.Skip(4).Take(4), row => { Assert.Equal("none", row.AgentUsedObject); Assert.Equal("none", row.PointUser); });
        Assert.False(trace[8].LeaseMatches);
        Assert.Equal("exact", trace[10].AgentUsedObject);
        Assert.Equal("exact", trace[10].PointUser);
        Assert.Equal(trace[3].Sequence, trace[5].EntrySequence);
        Assert.Equal(trace[8].Sequence, trace[10].EntrySequence);
    }

    [Fact]
    public void Trace_CallbackAloneDoesNotRetireLeaseOrConfirmRelease()
    {
        InstallNativeUseTrace();
        behavior.SetHeldHelm(0, true);
        behavior.OnObjectStoppedBeingUsed(agent, point);
        behavior.BeforeHeldHelmTick(machine);
        Assert.Same(agent, behavior.heldHelmAgent);
        Assert.Same(agent, point.UserAgent);
        var callback = behavior.HelmTrace.Last();
        Assert.Equal("stop:callback", callback.Phase);
        Assert.Equal("exact", callback.AgentUsedObject);
        Assert.Equal("exact", callback.PointUser);
        Assert.Equal("released", behavior.SetHeldHelm(0, false));
        Assert.Null(point.UserAgent);
    }

    [Fact]
    public void Trace_ExitWithoutNativeLifetimeReadsOnlyRetainedManagedIdentities()
    {
        var call = behavior.BeginHelmTrace(agent, point, "stop");
        Assert.NotNull(call);
        AccessTools.Field(typeof(Agent), "_pointer").SetValue(agent, UIntPtr.Zero);
        AccessTools.Field(typeof(ScriptComponentBehavior), "_gameEntity").SetValue(point, default(WeakGameEntity));
        AccessTools.PropertySetter(typeof(MissionBehavior), nameof(MissionBehavior.Mission)).Invoke(behavior, new object?[] { null });
        NavalLabBehavior.EndHelmTrace(call, null);
        Assert.Equal(2, behavior.HelmTrace.Length);
        Assert.Equal("unavailable:native_lifetime_not_proven;managed_identities_only", behavior.HelmTrace[1].NativeState);
        Assert.Equal("none", behavior.HelmTrace[1].AgentUsedObject);
        Assert.Equal("none", behavior.HelmTrace[1].PointUser);
    }

    [Fact]
    public void Trace_RecorderFailureDoesNotChangeRealUseStopOrLeaseRetirement()
    {
        InstallNativeUseTrace();
        AccessTools.Field(typeof(NavalLabBehavior), "helmTrace").SetValue(behavior, null);
        Assert.Equal("taken", behavior.SetHeldHelm(0, true));
        Assert.Same(agent, point.UserAgent);
        Assert.Equal("released", behavior.SetHeldHelm(0, false));
        Assert.Null(point.UserAgent);
        Assert.Null(agent.CurrentlyUsedGameObject);
        Assert.Null(behavior.heldHelmAgent);
        Assert.Null(behavior.Blocker);
    }

    private sealed class ThrowingStopBehavior : MissionLogic
    {
        internal readonly InvalidOperationException Failure = new("native callback failure");
        public override void OnObjectStoppedBeingUsed(Agent userAgent, UsableMissionObject usableGameObject) => throw Failure;
    }

    [Fact]
    public void Trace_RealStopCallbackExceptionPropagatesUnchangedAndExitObservesClearedManagedPair()
    {
        InstallNativeUseTrace();
        behavior.SetHeldHelm(0, true);
        var throwing = new ThrowingStopBehavior();
        mission.Instance.MissionBehaviors.Add(throwing);
        Assert.Same(throwing.Failure, Assert.Throws<TargetInvocationException>(NativeStop).InnerException);
        var exit = behavior.HelmTrace.Last();
        Assert.Equal("stop:exit", exit.Phase);
        Assert.Equal(typeof(InvalidOperationException).FullName, exit.ExceptionType);
        Assert.Equal("none", exit.AgentUsedObject);
        Assert.Equal("none", exit.PointUser);
        Assert.Same(agent, behavior.heldHelmAgent);
    }

    [Fact]
    public void Trace_CapsRecordsAndStacksWithoutAffectingRealUseOrRelease()
    {
        InstallNativeUseTrace();
        for (int i = 0; i < 20; i++)
        {
            Assert.Equal("taken", behavior.SetHeldHelm(0, true));
            Assert.Equal("released", behavior.SetHeldHelm(0, false));
        }
        Assert.Equal(NavalLabBehavior.HelmTraceLimit, behavior.HelmTrace.Length);
        Assert.Equal(20, behavior.helmReleaseCalls);
        Assert.Equal(20, behavior.helmUseCallbacks);
        Assert.All(behavior.HelmTrace, row =>
        {
            Assert.InRange(row.Stack.Length, 0, NavalLabBehavior.HelmTraceStackLimit);
            Assert.All(row.Stack, frame => Assert.InRange(frame.Length, 1, 180));
            Assert.Contains("managed_identities_only", row.NativeState);
        });
        Assert.NotEmpty(behavior.HelmTrace[0].Stack);
        Assert.Null(point.UserAgent);
        Assert.Null(agent.CurrentlyUsedGameObject);
    }

    [Theory]
    [InlineData("foreign-agent")]
    [InlineData("foreign-point")]
    [InlineData("normal")]
    [InlineData("inactive")]
    public void Trace_ExcludesNormalAndForeignUseWithoutChangingNativeExecution(string scope)
    {
        InstallNativeUseTrace();
        if (scope == "foreign-agent") behavior.Agents[0] = Shell<Agent>();
        if (scope == "foreign-point") Set(machine, nameof(UsableMachine.PilotStandingPoint), Shell<StandingPoint>());
        if (scope == "normal")
        {
            var old = behavior.manifest;
            behavior = new NavalLabBehavior(new NavalLabManifest(old.InstanceId, old.IncarnationId, old.Controllers, old.Combatants, old.Ships), "A", null!, null!)
                { Ships = behavior.Ships, Agents = behavior.Agents };
            NavalLabPhysicsPatches.Active = behavior;
            mission.Instance.MissionBehaviors.Clear();
            mission.Instance.MissionBehaviors.Add(behavior);
        }
        if (scope == "inactive")
        {
            NavalLabPhysicsPatches.Active = null;
            mission.Instance.MissionBehaviors.Clear();
        }
        NativeUse();
        Assert.Same(agent, point.UserAgent);
        Assert.Same(point, agent.CurrentlyUsedGameObject);
        NativeStop();
        Assert.Null(point.UserAgent);
        Assert.Null(agent.CurrentlyUsedGameObject);
        Assert.Empty(behavior.HelmTrace);
    }

    public void Dispose()
    {
        NavalLabPhysicsPatches.Active = null;
        harmony.UnpatchAll(harmony.Id);
        harmony.UnpatchAll("coop.warsails.lab.physics");
        mission.Dispose();
    }
}
#endif
