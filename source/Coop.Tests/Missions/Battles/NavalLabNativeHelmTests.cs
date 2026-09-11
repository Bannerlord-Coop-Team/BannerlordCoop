#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Common;
using HarmonyLib;
using Missions.Battles;
using Missions.Naval;
using NavalDLC.Missions.Objects;
using NavalDLC.Missions.Objects.UsableMachines;
using NavalDLC.View.MissionViews;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public sealed class NavalLabNativeHelmTests : IDisposable
{
    private readonly Harmony harmony = new("coop.tests.naval.native-helm");
    private readonly MissionCurrentScope scope = new();
    private static NavalLabNativeHelmTests current = null!;
    private NavalLabBehavior fixture = null!;
    private Agent agent = null!;
    private StandingPoint point = null!;
    private ShipControllerMachine machine = null!;
    private int uses;
    private int stops;
    private bool bypassPrecondition = true;
    private bool authority = true;
    private bool ready = true;
    private bool reserved;
    private bool throwDispatch;
    private bool mutateOnDispatch = true;
    private MissionScreen screen = null!;
    private float pointDistance;
    private bool eligible = true;
    private bool screenFocused = true;
    private const string Dispatch = "dispatched:synthetic_native_helm_pending_observation";

    public NavalLabNativeHelmTests()
    {
        current = this;
        Patch(AccessTools.Method(typeof(SoundEvent), nameof(SoundEvent.GetEventIdFromString)), nameof(Zero));
        Patch(AccessTools.Method(typeof(MBAnimation), nameof(MBAnimation.GetActionCodeWithName)), nameof(Zero));
        Patch(AccessTools.PropertyGetter(typeof(GameThread), nameof(GameThread.IsGameThread)), nameof(True));
        Patch(AccessTools.Method(typeof(Agent), nameof(Agent.IsActive)), nameof(True));
        Patch(AccessTools.PropertyGetter(typeof(Agent), nameof(Agent.IsPlayerControlled)), nameof(True));
        Patch(AccessTools.PropertyGetter(typeof(Agent), nameof(Agent.IsAIControlled)), nameof(False));
        Patch(AccessTools.PropertyGetter(typeof(Mission), nameof(Mission.MainAgent)), nameof(Main));
        Patch(AccessTools.PropertyGetter(typeof(GameNetwork), nameof(GameNetwork.IsClient)), nameof(False));
        Patch(AccessTools.PropertyGetter(typeof(GameNetwork), nameof(GameNetwork.IsClientOrReplay)), nameof(False));
        Patch(AccessTools.PropertyGetter(typeof(UsableMissionObject), nameof(UsableMissionObject.HasAIMovingTo)), nameof(Reserved));
        Patch(AccessTools.PropertyGetter(typeof(UsableMissionObject), nameof(UsableMissionObject.MovingAgent)), nameof(NoAgent));
        Patch(AccessTools.PropertyGetter(typeof(NavalLabBehavior), "CanUseNativeControls"), nameof(Authority));
        Patch(AccessTools.PropertyGetter(typeof(MissionShip), nameof(MissionShip.IsDeployed)), nameof(Ready));
        Patch(AccessTools.Method(typeof(NavalLabBehavior), "NativeHelmPrecondition"), nameof(Precondition));
        Patch(AccessTools.Method(typeof(Agent), nameof(Agent.UseGameObject)), nameof(UseBoundary));
        Patch(AccessTools.Method(typeof(Agent), nameof(Agent.StopUsingGameObject)), nameof(StopBoundary));
        Patch(AccessTools.PropertySetter(typeof(Agent), nameof(Agent.MovementInputVector)), nameof(Skip));
        Patch(AccessTools.Method(typeof(ShipControllerMachine), nameof(ShipControllerMachine.OnPilotAssignedDuringSpawn)), nameof(ForbiddenPlacement));
        var id = Guid.NewGuid();
        var manifest = new NavalLabManifest("naval-lab:" + id.ToString("N"), id, new[] { "A", "B" },
            Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray(), new[] { Guid.NewGuid(), Guid.NewGuid() }, NavalLabMode.TwoClientNative);
        fixture = new NavalLabBehavior(manifest, "A", null!, null!);
        AccessTools.PropertySetter(typeof(MissionBehavior), nameof(MissionBehavior.Mission)).Invoke(fixture, new object[] { scope.Instance });
        var ship = Shell<MissionShip>(); machine = Shell<ShipControllerMachine>(); point = Shell<StandingPoint>(); agent = Shell<Agent>();
        Entity(ship, 123); Entity(point, 124);
        Set(agent, "_pointer", new UIntPtr(456));
        Set(machine, "<PilotStandingPoint>k__BackingField", point); Set(machine, "<AttachedShip>k__BackingField", ship);
        Set(ship, "<ShipControllerMachine>k__BackingField", machine);
        Set(ship, "<ShipOrigin>k__BackingField", Shell<NavalLabShipOrigin>());
        var formation = Shell<Formation>(); Set(ship, "<Formation>k__BackingField", formation); Set(agent, "_formation", formation);
        fixture.Ships = new[] { ship, Shell<MissionShip>() }; fixture.Agents = new Agent[10]; fixture.Agents[0] = agent;
        fixture.nativeDeploymentCallbacks = 1;
        Set(scope.Instance, "<MissionBehaviors>k__BackingField", new List<MissionBehavior> { fixture });
    }
    private void Patch(MethodBase method, string prefix) => harmony.Patch(method, prefix: new HarmonyMethod(GetType(), prefix));
    private static bool Zero(ref int __result) { __result = 0; return false; }
    private static bool True(ref bool __result) { __result = true; return false; }
    private static bool False(ref bool __result) { __result = false; return false; }
    private static bool Skip() => false;
    private static bool ForbiddenPlacement() => throw new InvalidOperationException("spawn shortcut is forbidden");
    private static bool Authority(ref bool __result) { __result = current.authority; return false; }
    private static bool Ready(ref bool __result) { __result = current.ready; return false; }
    private static bool Reserved(ref bool __result) { __result = current.reserved; return false; }
    private static bool NoAgent(ref Agent? __result) { __result = null; return false; }
    private static bool Main(ref Agent __result) { __result = current.agent; return false; }
    private static bool Precondition(ref string? __result) { __result = null; return !current.bypassPrecondition; }
    private static void Set(object target, string name, object? value) => AccessTools.Field(target.GetType(), name).SetValue(target, value);
    private static T Shell<T>()
    {
        var managed = typeof(ScriptComponentBehavior).BaseType!.Assembly.GetType("TaleWorlds.DotNet.Managed")!;
        var field = AccessTools.Field(managed, "_moduleTypes"); var previous = field.GetValue(null);
        try { if (previous == null) field.SetValue(null, new Dictionary<string, Type>()); return (T)FormatterServices.GetUninitializedObject(typeof(T)); }
        finally { field.SetValue(null, previous); }
    }
    private static void Entity(ScriptComponentBehavior target, ulong pointer) => Set(target, "_gameEntity",
        Activator.CreateInstance(typeof(WeakGameEntity), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { new UIntPtr(pointer) }, null));
    private void Occupancy(bool user, bool used)
    {
        Set(point, "_userAgent", user ? agent : null);
        Set(agent, "<CurrentlyUsedGameObject>k__BackingField", used ? point : null);
    }
    private static bool UseBoundary(Agent __instance, UsableMissionObject usedObject)
    {
        Assert.Same(current.agent, __instance); Assert.Same(current.point, usedObject); current.uses++;
        if (current.mutateOnDispatch) current.Occupancy(true, true);
        current.fixture.OnObjectUsed(__instance, usedObject);
        if (current.throwDispatch) throw new InvalidOperationException("uncertain native take");
        return false;
    }
    private static bool StopBoundary(Agent __instance, bool isSuccessful)
    {
        Assert.Same(current.agent, __instance); Assert.False(isSuccessful); current.stops++;
        if (current.mutateOnDispatch) current.Occupancy(false, false);
        current.fixture.OnObjectStoppedBeingUsed(__instance, current.point);
        if (current.throwDispatch) throw new InvalidOperationException("uncertain native stop");
        return false;
    }
    private void Tick() => fixture.TickAgentControl(0.01f);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RealNativeHandleActionDispatchesOnce_CompletionRequiresLaterTick_NotCallbacks(bool take)
    {
        Occupancy(!take, !take);
        var id = Guid.NewGuid();
        Assert.Equal(Dispatch, fixture.RequestNativeHelm(id, 0, take));
        Assert.Equal("pending", fixture.nativeHelmPhase);
        fixture.OnObjectUsed(agent, point); fixture.OnObjectStoppedBeingUsed(agent, point);
        Assert.Equal("pending", fixture.nativeHelmPhase);
        Assert.Equal(Dispatch, fixture.RequestNativeHelm(id, 0, take));
        Assert.Equal("rejected:native_helm_pending", fixture.RequestNativeHelm(Guid.NewGuid(), 0, !take));
        Assert.Equal(id, fixture.nativeHelmOperation);
        Tick(); Assert.Equal(take ? "observed_taken" : "observed_released", fixture.nativeHelmPhase);
        Assert.True(fixture.nativeHelmObservedTick > fixture.nativeHelmDispatchTick);
        Assert.Equal(Dispatch, fixture.RequestNativeHelm(id, 0, take));
        Assert.Equal(Dispatch, fixture.nativeHelmReceipt);
        Assert.Equal(1, uses + stops);
        Assert.Null(fixture.Blocker);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void TakeRequiresBothExactOccupancyIdentities(bool user, bool used)
    {
        mutateOnDispatch = false;
        fixture.RequestNativeHelm(Guid.NewGuid(), 0, true);
        Occupancy(user, used); Tick(); Assert.Equal("pending", fixture.nativeHelmPhase);
        Occupancy(true, true); Tick(); Assert.Equal("observed_taken", fixture.nativeHelmPhase);
        Assert.Equal(1, uses); Assert.Equal(0, stops);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ReleaseCallbackNeverMeansClear_PartialOrUnchangedOccupancyTimesOutWithoutRedispatch(bool user, bool used)
    {
        Occupancy(true, true); mutateOnDispatch = false;
        var id = Guid.NewGuid(); Assert.Equal(Dispatch, fixture.RequestNativeHelm(id, 0, false));
        Occupancy(user, used); Tick(); Assert.Equal("pending", fixture.nativeHelmPhase);
        fixture.nativeHelmDeadline = 0; Tick(); Assert.Equal("failed", fixture.nativeHelmPhase);
        Assert.Equal("observation_timeout", fixture.nativeHelmFailure);
        fixture.Hold(); fixture.CancelControls(); Assert.True(fixture.factoryTerminal);
        Assert.Equal(1, stops); Assert.Equal(0, uses);
        Assert.Equal(Dispatch, fixture.RequestNativeHelm(id, 0, false));
        Assert.Equal("rejected:terminal_hold", fixture.RequestNativeHelm(Guid.NewGuid(), 0, false));
        Assert.Equal(user, point.UserAgent == agent); Assert.Equal(used, agent.CurrentlyUsedGameObject == point);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NoOccupationExpiry_AndNativeExceptionNeverRetriesOrClaimsRollback(bool throws)
    {
        throwDispatch = throws;
        var id = Guid.NewGuid(); string receipt = fixture.RequestNativeHelm(id, 0, true);
        if (throws)
        {
            Assert.StartsWith("failed:", receipt); fixture.Hold();
            Assert.Equal("failed", fixture.nativeHelmPhase); Assert.Same(agent, point.UserAgent);
        }
        else
        {
            Tick(); fixture.nativeHelmDeadline = 0; Tick();
            Assert.Equal("observed_taken", fixture.nativeHelmPhase); Assert.Same(agent, point.UserAgent);
        }
        Assert.Equal(receipt, fixture.RequestNativeHelm(id, 0, true)); Assert.Equal(1, uses); Assert.Equal(0, stops);
    }

    [Theory]
    [InlineData("foreign_user")]
    [InlineData("foreign_object")]
    [InlineData("replacement_point")]
    [InlineData("authority")]
    [InlineData("readiness")]
    [InlineData("terminal")]
    public void ChangedIdentityOrLifetimeFailsPendingObservationWithoutRepair(string condition)
    {
        fixture.RequestNativeHelm(Guid.NewGuid(), 0, true);
        if (condition == "foreign_user") Set(point, "_userAgent", Shell<Agent>());
        if (condition == "foreign_object") Set(agent, "<CurrentlyUsedGameObject>k__BackingField", Shell<StandingPoint>());
        if (condition == "replacement_point") Set(machine, "<PilotStandingPoint>k__BackingField", Shell<StandingPoint>());
        if (condition == "authority") authority = false;
        if (condition == "readiness") ready = false;
        if (condition == "terminal") fixture.CancelControls();
        Tick(); Assert.Equal("failed", fixture.nativeHelmPhase); Assert.NotNull(fixture.Blocker);
        fixture.Hold(); Assert.Equal(1, uses); Assert.Equal(0, stops);
    }

    [Theory]
    [InlineData("foreign_user")]
    [InlineData("foreign_object")]
    [InlineData("reserved")]
    [InlineData("authority")]
    [InlineData("readiness")]
    [InlineData("contradiction")]
    [InlineData("view_unavailable")]
    public void ActualPreflightRejectsBeforeDispatch_AndNeverDisplacesOccupants(string condition)
    {
        bypassPrecondition = false;
        if (condition == "foreign_user") Set(point, "_userAgent", Shell<Agent>());
        if (condition == "foreign_object") Set(agent, "<CurrentlyUsedGameObject>k__BackingField", Shell<StandingPoint>());
        if (condition == "reserved") reserved = true;
        if (condition == "authority") authority = false;
        if (condition == "readiness") ready = false;
        if (condition == "contradiction") Occupancy(true, false);
        string result = fixture.RequestNativeHelm(Guid.NewGuid(), 0, true);
        Assert.StartsWith(condition == "contradiction" ? "failed:" : "rejected:", result);
        Assert.False(fixture.nativeHelmDispatched); Assert.NotEqual("pending", fixture.nativeHelmPhase);
        Assert.Equal(0, uses + stops);
    }

    private static bool Screen(ref MissionScreen __result) { __result = current.screen; return false; }
    private static bool Top(ref ScreenBase __result) { __result = current.screenFocused ? current.screen : null!; return false; }
    private static bool Position(ref Vec3 __result) { __result = Vec3.Zero; return false; }
    private static bool PointPosition(ref Vec3 __result) { __result = new Vec3(current.pointDistance, 0, 0); return false; }
    private static bool Eligible(ref bool __result) { __result = current.eligible; return false; }
    private static bool Disabled(ref bool __result) { __result = !current.eligible; return false; }
    private static bool UserFrame(ref WorldFrame __result)
    {
        var position = default(WorldPosition);
        position.SetVec3(UIntPtr.Zero, new Vec3(current.pointDistance, 0, 0), true);
        __result = new WorldFrame(Mat3.Identity, position);
        return false;
    }

    [Theory]
    [InlineData("allowed")]
    [InlineData("range")]
    [InlineData("focus")]
    [InlineData("availability")]
    [InlineData("screen")]
    [InlineData("window")]
    [InlineData("missing_component")]
    public void ActualPreflightPreservesNativeRangeFocusAvailabilityAndViewGates(string condition)
    {
        bypassPrecondition = false;
        var controller = Shell<MissionMainAgentController>();
        var interaction = new MissionMainAgentInteractionComponent(controller);
        Set(controller, "InteractionComponent", interaction); Set(controller, "_activated", true);
        Set(interaction, "<CurrentFocusedObject>k__BackingField", point);
        Set(interaction, "_currentInteractableObject", condition == "focus" ? null : point);
        screen = Shell<MissionScreen>();
        Set(scope.Instance, "<MissionBehaviors>k__BackingField", new List<MissionBehavior> { fixture, controller, Shell<MissionShipControlView>() });
        Patch(AccessTools.PropertyGetter(typeof(MissionView), nameof(MissionView.MissionScreen)), nameof(Screen));
        Patch(AccessTools.PropertyGetter(typeof(ScreenManager), nameof(ScreenManager.TopScreen)), nameof(Top));
        Patch(AccessTools.PropertyGetter(typeof(MissionScreen), nameof(MissionScreen.IsCheatGhostMode)), nameof(False));
        Patch(AccessTools.PropertyGetter(typeof(MissionScreen), nameof(MissionScreen.IsPhotoModeEnabled)), nameof(False));
        Patch(AccessTools.PropertyGetter(typeof(MissionScreen), nameof(MissionScreen.IsRadialMenuActive)), nameof(False));
        Patch(AccessTools.PropertyGetter(typeof(MissionShipControlView), "IsDisplayingADialog"), nameof(False));
        Patch(AccessTools.PropertyGetter(typeof(Mission), nameof(Mission.IsMainAgentItemInteractionEnabled)), nameof(True));
        Patch(AccessTools.PropertyGetter(typeof(Mission), nameof(Mission.IsMainAgentObjectInteractionEnabled)), nameof(True));
        Patch(AccessTools.PropertyGetter(typeof(Agent), nameof(Agent.HasMount)), nameof(False));
        Patch(AccessTools.Method(typeof(Agent), nameof(Agent.IsAbleToUseMachine)), nameof(True));
        Patch(AccessTools.PropertyGetter(typeof(UsableMissionObject), nameof(UsableMissionObject.IsFocusable)), nameof(True));
        Patch(AccessTools.Method(typeof(Agent), nameof(Agent.CanUseObject)), nameof(Eligible));
        Patch(AccessTools.Method(typeof(StandingPoint), nameof(StandingPoint.GetUserFrameForAgent)), nameof(UserFrame));
        Patch(AccessTools.Method(typeof(UsableMissionObject), nameof(UsableMissionObject.IsDisabledForAgent)), nameof(Disabled));
        Set(machine, "<StandingPoints>k__BackingField", new MBList<StandingPoint> { point });
        Patch(AccessTools.Method(typeof(ShipControllerMachine), nameof(ShipControllerMachine.IsAttachedShipVacant)), nameof(False));
        Patch(AccessTools.PropertyGetter(typeof(Agent), nameof(Agent.Position)), nameof(Position));
        Patch(AccessTools.PropertyGetter(typeof(WeakGameEntity), nameof(WeakGameEntity.GlobalPosition)), nameof(PointPosition));
        pointDistance = condition == "range" ? 100 : 0;
        eligible = condition != "availability"; screenFocused = condition != "screen";
        bool originalFocus = ScreenManager._isWindowFocused;
        try
        {
            ScreenManager._isWindowFocused = condition != "window";
            if (condition == "missing_component") Set(controller, "InteractionComponent", null);
            Assert.Equal(condition switch
            {
                "window" => "window_not_focused", "screen" => "mission_screen_not_on_top",
                "missing_component" => "interaction_component_missing", _ => null
            }, fixture.NativeInteractionViewBlocker());
            if (condition == "allowed" || condition == "range" || condition == "focus")
                Assert.Equal(condition != "range", machine.GetValidVacantReachableStandingPointForAgent(agent) == point.GameEntity);
            string result = fixture.RequestNativeHelm(Guid.NewGuid(), 0, true);
            Assert.Equal(condition switch
            {
                "allowed" => Dispatch, "range" => "rejected:native_helm_not_reachable",
                "focus" => "rejected:native_helm_not_focused", "availability" => "rejected:native_use_ineligible",
                _ => "rejected:native_interaction_view_unavailable"
            }, result);
            Assert.Equal(condition == "allowed" ? 1 : 0, uses); Assert.Equal(0, stops);
            Assert.Same(condition == "focus" ? null : point, interaction._currentInteractableObject);
        }
        finally { ScreenManager._isWindowFocused = originalFocus; }
    }

    [Fact]
    public void LastOutcomeRemainsReadableDuringRejectedOrPendingNextOperation()
    {
        var taken = Guid.NewGuid();
        fixture.RequestNativeHelm(taken, 0, true); Tick();
        var outcome = fixture.nativeHelmLastOutcome;
        Assert.NotNull(outcome);
        Assert.Contains(taken.ToString(), Newtonsoft.Json.JsonConvert.SerializeObject(outcome));
        bypassPrecondition = false;
        Assert.StartsWith("rejected:", fixture.RequestNativeHelm(Guid.NewGuid(), 0, true));
        Assert.Same(outcome, fixture.nativeHelmLastOutcome);
        bypassPrecondition = true;
        fixture.RequestNativeHelm(Guid.NewGuid(), 0, false);
        Assert.Same(outcome, fixture.nativeHelmLastOutcome);
        Tick(); Assert.NotSame(outcome, fixture.nativeHelmLastOutcome);
        Assert.Contains("observed_released", Newtonsoft.Json.JsonConvert.SerializeObject(fixture.nativeHelmLastOutcome));
        Assert.Equal(1, uses); Assert.Equal(1, stops);
    }

    [Fact]
    public void WrongSlotIsRejectedBeforeAnyRequestIsRecorded()
    {
        Assert.Equal("rejected:operation_or_owner", fixture.RequestNativeHelm(Guid.NewGuid(), 1, true));
        Assert.Equal(Guid.Empty, fixture.nativeHelmOperation); Assert.Equal(0, uses + stops);
    }
    public void Dispose() { harmony.UnpatchAll(harmony.Id); scope.Dispose(); }
}
#endif
