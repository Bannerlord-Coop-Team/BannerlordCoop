#if DEBUG
using Common.Messaging;
using Common.LiveTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Missions.Battles;
using Missions;
using Missions.Messages;
using Moq;
using HarmonyLib;
using Xunit;
using System.Runtime.Serialization;
using TaleWorlds.Library;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Objects.Usables;
using TaleWorlds.Core;

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public class SiegeInteractionDebugBehaviorTests
{
    [Fact]
    public void MachineObservation_ExplicitPeerTargetDoesNotChangeStagedTargetOrHideDuplicates()
    {
        var harmony = new Harmony("coop.tests.siege-observed-machine");
        try
        {
            harmony.Patch(AccessTools.Method(typeof(ScriptComponentBehavior), "CacheEditableFieldsForAllScriptComponents"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(SiegeInteractionDebugBehaviorTests), nameof(SkipScriptComponentCache))));
#pragma warning disable SYSLIB0050
            var ladder = (UsableMachine)FormatterServices.GetUninitializedObject(typeof(SiegeLadder));
            var stone = (UsableMachine)FormatterServices.GetUninitializedObject(typeof(StonePile));
#pragma warning restore SYSLIB0050
            AccessTools.Property(typeof(MissionObject), "Id").SetValue(ladder, new MissionObjectId(915, false));
            AccessTools.Property(typeof(MissionObject), "Id").SetValue(stone, new MissionObjectId(142, false));
            var behavior = new SiegeInteractionDebugBehavior(Mock.Of<IMessageBroker>());
            var machines = new[] { ladder, stone };
            Assert.Equal(machines, behavior.SelectObservedMachines(machines));
            AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "observedMachineId").SetValue(behavior, 915);
            Assert.Same(stone, Assert.Single(behavior.SelectObservedMachines(machines, 142)));
            Assert.Same(ladder, Assert.Single(behavior.SelectObservedMachines(machines)));
            Assert.Empty(behavior.SelectObservedMachines(machines, 999));
            Assert.Equal(2, behavior.SelectObservedMachines(new[] { stone, stone }, 142).Count());
            Assert.Null(AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "requestId").GetValue(behavior));
            Assert.False((bool)AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "pressInvoked").GetValue(behavior));
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }

    [Theory]
    [InlineData("handler-use")]
    [InlineData("handler-stop")]
    [InlineData("handler-fire")]
    [InlineData("handler-reload")]
    public void FunctionalAction_WithoutCapturedActorRejectsWithoutArmingInput(string action)
    {
        var behavior = new SiegeInteractionDebugBehavior(Mock.Of<IMessageBroker>());
        var request = new NetworkSiegeInteractionDebugRequest("session", "testclient", "once", 337, action, 0);
        AccessTools.Method(typeof(SiegeInteractionDebugBehavior), "ApplyFunctionalAction")
            .Invoke(behavior, new object[] { request, null, null });
        Assert.Equal("fixture_handler_rejected", AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "status").GetValue(behavior));
        Assert.Null(AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "functionalAction").GetValue(behavior));
        foreach (string flag in new[] { "pressInvoked", "externalInputArmed", "edgeObserved", "edgeCleared" })
            Assert.False((bool)AccessTools.Field(typeof(SiegeInteractionDebugBehavior), flag).GetValue(behavior));
        Assert.Empty(JObject.FromObject(behavior.ReadUseDispatch())["samples"]);
    }

    [Fact]
    public void UseDispatchHooks_InstallOnImplementedTargetsAndPreserveResultsAndExceptions()
    {
        using var mission = new MissionCurrentScope();
        var patch = typeof(SiegeInteractionDebugBehavior.UseDispatchObservationPatch);
        var targets = SiegeInteractionDebugBehavior.UseDispatchObservationPatch.TargetMethods().ToArray();
        Assert.Equal(4, targets.Length);
        Assert.All(targets, target => Assert.NotNull(target?.GetMethodBody()));
        Assert.Contains(MissionModule.CreatePatchCategoryRegistrations(), registration =>
            registration.Assembly == patch.Assembly && registration.Category == "CoopSiegeInteractionDebug");
        var prefix = AccessTools.Method(patch, "Prefix");
        var finalizer = AccessTools.Method(patch, "Finalizer");
        Assert.Equal(typeof(void), prefix.ReturnType);
        Assert.Equal(typeof(void), finalizer.ReturnType);
        var harmony = new Harmony("coop.tests.siege-use-dispatch-observer");
        try
        {
            harmony.CreateClassProcessor(patch).Patch();
            Assert.All(targets, target => Assert.Contains(Harmony.GetPatchInfo(target).Finalizers,
                installed => installed.owner == harmony.Id));
            harmony.Patch(AccessTools.Method(typeof(SiegeInteractionDebugBehaviorTests), nameof(DispatchResult)),
                prefix: new HarmonyMethod(prefix), finalizer: new HarmonyMethod(finalizer));
            Assert.Equal(42, DispatchResult(null));
            var failure = new InvalidOperationException("original dispatch failure");
            Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => DispatchResult(failure)));
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private int DispatchResult(Exception failure)
    {
        if (failure != null) throw failure;
        return 42;
    }

    [Fact]
    public void UseDispatchObservation_InactiveObserverDoesNotReadNativeStateOrSatisfyInput()
    {
        var behavior = new SiegeInteractionDebugBehavior(Mock.Of<IMessageBroker>());
        Assert.Equal(0, behavior.ObserveUseDispatch(null, "UseGameObject", null));
        Assert.Empty(JObject.FromObject(behavior.ReadUseDispatch())["samples"]);
        Assert.False((bool)AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "edgeObserved").GetValue(behavior));
        Assert.False((bool)AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "edgeCleared").GetValue(behavior));
    }

    [Fact]
    public void UseDispatchObservation_BoundsEvidenceAndKeepsTheFirstEntryStopSequence()
    {
        var behavior = new SiegeInteractionDebugBehavior(Mock.Of<IMessageBroker>());
        var phases = new[] { "focus-entry", "use-entry", "stop-entry", "stop-exit", "use-exit", "focus-exit" };
        foreach (var phase in phases) behavior.AppendUseDispatch(new { phase });
        var first = JObject.FromObject(behavior.ReadUseDispatch());
        for (int index = phases.Length; index < 30; index++) behavior.AppendUseDispatch(new { phase = "later" });
        var result = JObject.FromObject(behavior.ReadUseDispatch());
        Assert.Equal(phases, result["samples"].Take(phases.Length).Select(sample => sample["phase"].Value<string>()));
        Assert.Equal(phases.Length, first["samples"].Count());
        Assert.Equal(16, result["samples"].Count());
        Assert.Equal(14, result["dropped"].Value<int>());
    }

    [Fact]
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void UseDispatchObservation_OffThreadStopRetainsArgumentsAndCallerWithoutNativeReads()
    {
        using var mission = new MissionCurrentScope();
        var behavior = new SiegeInteractionDebugBehavior(Mock.Of<IMessageBroker>());
#pragma warning disable SYSLIB0050
        var agent = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
        var otherAgent = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
        var point = (StandingPoint)FormatterServices.GetUninitializedObject(typeof(StandingPoint));
#pragma warning restore SYSLIB0050
        AccessTools.Property(typeof(MissionBehavior), "Mission").SetValue(behavior, mission.Instance);
        AccessTools.Field(typeof(Mission), "_mainAgent").SetValue(mission.Instance, agent);
        AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "capturedAgent").SetValue(behavior, agent);
        AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "useDispatchPoint").SetValue(behavior, point);
        AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "useDispatchThreadId").SetValue(behavior, -1);
        AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "requestId").SetValue(behavior, "ballista-use");
        AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "useDispatchRequestId").SetValue(behavior, "ballista-use");
        var flags = Agent.StopUsingGameObjectFlags.DoNotWieldWeaponAfterStoppingUsingGameObject;
        var arguments = new object[] { false, flags };
        string method = "StopUsingGameObjectAux";
        Assert.Equal(0, behavior.ObserveUseDispatch(otherAgent, method, arguments));
        Assert.Equal(0, behavior.ObserveUseDispatch(agent, method, arguments, expectedRequestId: "stale"));
        var before = DateTime.UtcNow;

        int call = behavior.ObserveUseDispatch(agent, method, arguments);
        var failure = new InvalidOperationException("original stop failure");
        behavior.ObserveUseDispatch(agent, method, arguments, call, failure, "ballista-use");

        var samples = JObject.FromObject(behavior.ReadUseDispatch())["samples"].ToArray();
        Assert.Equal(2, samples.Length);
        Assert.Equal(call, samples[1]["call"].Value<int>());
        Assert.Equal("non_game_thread", samples[0]["observationError"].Value<string>());
        Assert.False(samples[0]["stop"]["isSuccessful"].Value<bool>());
        Assert.Equal((int)flags, samples[0]["stop"]["flags"].Value<int>());
        var callers = samples[0]["stop"]["callers"].Values<string>().ToArray();
        Assert.InRange(callers.Length, 1, 8);
        Assert.All(callers, caller => Assert.InRange(caller.Length, 1, 256));
        Assert.Contains(callers, caller => caller.Contains(nameof(UseDispatchObservation_OffThreadStopRetainsArgumentsAndCallerWithoutNativeReads)));
        Assert.InRange(samples[0]["recordedUtc"].Value<DateTime>().ToUniversalTime(), before, DateTime.UtcNow);
        Assert.Equal(typeof(InvalidOperationException).FullName, samples[1]["exception"].Value<string>());
        Assert.Equal(JTokenType.Null, samples[1]["stop"].Type);
        Assert.Null(samples[0]["usingObject"]);
        Assert.Equal(new object[] { false, flags }, arguments);

        int laterCall = behavior.ObserveUseDispatch(agent, method, new object[] { true, flags });
        var later = JObject.FromObject(behavior.ReadUseDispatch())["samples"].Last;
        Assert.Equal(laterCall, later["call"].Value<int>());
        Assert.True(later["stop"]["isSuccessful"].Value<bool>());
        Assert.Equal((int)flags, later["stop"]["flags"].Value<int>());
        Assert.Equal(JTokenType.Null, later["stop"]["callers"].Type);

        AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "requestId").SetValue(behavior, "next-ballista-use");
        AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "useDispatchRequestId").SetValue(behavior, "next-ballista-use");
        Assert.Equal(0, behavior.ObserveUseDispatch(agent, method, arguments, expectedRequestId: "ballista-use"));
        behavior.ObserveUseDispatch(agent, method, arguments);
        var next = JObject.FromObject(behavior.ReadUseDispatch())["samples"].Last;
        Assert.Equal("next-ballista-use", next["requestId"].Value<string>());
        Assert.NotEmpty(next["stop"]["callers"]);
        Assert.False((bool)AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "edgeObserved").GetValue(behavior));
        Assert.False((bool)AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "edgeCleared").GetValue(behavior));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("wrong-id")]
    [InlineData("wrong-mission")]
    [InlineData("null-agent")]
    public void ObservedAgent_RejectsIncompleteOrStaleIdentityBeforeNativeReads(string failure)
    {
        using var mission = new MissionCurrentScope();
        var behavior = new SiegeInteractionDebugBehavior(Mock.Of<IMessageBroker>());
        AccessTools.Property(typeof(MissionBehavior), "Mission").SetValue(behavior, mission.Instance);
#pragma warning disable SYSLIB0050
        var agent = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
#pragma warning restore SYSLIB0050
        if (failure != "wrong-mission") AccessTools.Property(typeof(Agent), "Mission").SetValue(agent, mission.Instance);
        var id = Guid.NewGuid();
        var info = new CoopAgentInfo("testclient2", "testclient2", "siege-session",
            failure == "null-agent" ? null : agent, failure == "wrong-id" ? Guid.NewGuid() : id, 1);
        var registry = new Mock<INetworkAgentRegistry>(MockBehavior.Strict);
        registry.Setup(value => value.TryGetAgentInfo(id, out info)).Returns(failure != "missing");

        var result = JObject.FromObject(behavior.ReadObservedAgent(id, registry.Object));

        Assert.False(result["available"].Value<bool>());
        Assert.Equal(id.ToString("N"), result["agentId"].Value<string>());
        Assert.Null(result["actions"]);
        Assert.Null(result["usingObject"]);
    }

    [Fact]
    public void ObservedAgent_ReportsBothChannelsForTheExactReplicaWithoutMutation()
    {
        using var mission = new MissionCurrentScope();
        var behavior = new SiegeInteractionDebugBehavior(Mock.Of<IMessageBroker>());
        AccessTools.Property(typeof(MissionBehavior), "Mission").SetValue(behavior, mission.Instance);
#pragma warning disable SYSLIB0050
        var agent = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
#pragma warning restore SYSLIB0050
        AccessTools.Property(typeof(Agent), "Mission").SetValue(agent, mission.Instance);
        var id = Guid.NewGuid();
        var info = new CoopAgentInfo("testclient2", "testclient2", "siege-session", agent, id, 1);
        var registry = new Mock<INetworkAgentRegistry>(MockBehavior.Strict);
        registry.Setup(value => value.TryGetAgentInfo(id, out info)).Returns(true);
        var harmony = new Harmony("coop.tests.siege-observed-agent");
        AccessTools.Property(typeof(Agent), nameof(Agent.Equipment)).SetValue(agent, new MissionEquipment());
        try
        {
            // ActionIndexCache initializes named actions before the observer reads its indices.
            harmony.Patch(AccessTools.Method(typeof(MBAnimation), nameof(MBAnimation.GetActionCodeWithName)),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(SiegeInteractionDebugBehaviorTests), nameof(ObservedActionCode))));
            harmony.Patch(AccessTools.Method(typeof(Agent), nameof(Agent.IsActive)),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(SiegeInteractionDebugBehaviorTests), nameof(ObservedAgentActive))));
            harmony.Patch(AccessTools.DeclaredPropertyGetter(typeof(Agent), nameof(Agent.IsUsingGameObject)),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(SiegeInteractionDebugBehaviorTests), nameof(ObservedAgentUnused))));
            harmony.Patch(AccessTools.Method(typeof(Agent), nameof(Agent.GetCurrentAction)),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(SiegeInteractionDebugBehaviorTests), nameof(ObservedAgentAction))));
            harmony.Patch(AccessTools.Method(typeof(Agent), nameof(Agent.GetCurrentActionType)),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(SiegeInteractionDebugBehaviorTests), nameof(ObservedAgentActionType))));
            var before = DateTime.UtcNow;
            var result = JObject.FromObject(behavior.ReadObservedAgent(id, registry.Object));

            Assert.True(result["available"].Value<bool>());
            Assert.Equal(id.ToString("N"), result["agentId"].Value<string>());
            Assert.Equal("testclient2", result["originalOwner"].Value<string>());
            Assert.Equal("testclient2", result["currentAuthority"].Value<string>());
            Assert.Equal(new[] { 0, 1 }, result["actions"].Select(action => action["channel"].Value<int>()));
            Assert.Equal(new[] { 101, 202 }, result["actions"].Select(action => action["index"].Value<int>()));
            Assert.InRange(result["recordedUtc"].Value<DateTime>().ToUniversalTime(), before, DateTime.UtcNow);
            Assert.False(result["usingObject"].Value<bool>());
            Assert.All(result["equipment"], slot => Assert.Null(slot["itemId"].Value<string>()));
            Assert.Null(agent.CurrentlyUsedGameObject);
            Assert.Same(mission.Instance, agent.Mission);
            registry.Verify(value => value.TryGetAgentInfo(id, out info), Times.Once);
            registry.VerifyNoOtherCalls();
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }

    private static bool ObservedActionCode(ref int __result) { __result = -1; return false; }
    private static bool ObservedAgentUnused(ref bool __result) { __result = false; return false; }
    private static bool ObservedAgentActive(ref bool __result) { __result = true; return false; }
    private static bool ObservedAgentAction(int channelNo, ref ActionIndexCache __result)
    {
        object action = default(ActionIndexCache);
        AccessTools.Field(typeof(ActionIndexCache), "<Index>k__BackingField")
            .SetValue(action, channelNo == 0 ? 101 : 202);
        __result = (ActionIndexCache)action;
        return false;
    }
    private static bool ObservedAgentActionType(ref Agent.ActionCodeType __result) { __result = default; return false; }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ObservationPosition_SerializesOnlyUniqueCoordinatesOrNull(bool agentPresent)
    {
        Vec3? position = agentPresent ? new Vec3(12.5f, -3.25f, 61f) : (Vec3?)null;
        var json = JsonConvert.SerializeObject(SiegeInteractionDebugBehavior.DescribePosition(position));
        if (!agentPresent)
        {
            Assert.Equal("null", json);
            return;
        }

        var result = JObject.Parse(json);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in result.Properties()) Assert.True(names.Add(property.Name));
        Assert.Equal(new[] { "x", "y", "z" }, result.Properties().Select(property => property.Name));
        Assert.Equal(12.5f, result["x"].Value<float>());
        Assert.Equal(-3.25f, result["y"].Value<float>());
        Assert.Equal(61f, result["z"].Value<float>());
    }

    [Theory]
    [InlineData(1.6f)]
    [InlineData(2.2f)]
    public void NativeStoneStaging_AimsAtLowTargetInsteadOfHorizontalStandingFrame(float eyeHeight)
    {
        var userPosition = new Vec3(498.52f, 720.788f, 36.16533f);
        var targetCenter = userPosition + new Vec3(0.5f, 1f, 0.25f);
        var eye = userPosition + (Vec3.Up * eyeHeight);
        var direction = SiegeInteractionDebugBehavior.GetNativeStagingDirection(userPosition, eyeHeight, targetCenter);

        Assert.True(direction.z < -0.5f);
        Assert.True(Math.Abs(direction.Length - 1f) < 0.0001f);
        var targetDistance = (targetCenter - eye).Length;
        Assert.True((eye + (direction * targetDistance) - targetCenter).Length < 0.0001f);
        var horizontal = new Vec3(direction.x, direction.y, 0f).NormalizedCopy();
        Assert.True((eye + (horizontal * targetDistance) - targetCenter).Length > 1f);
    }

    [Theory]
    [InlineData(1.6f)]
    [InlineData(2.2f)]
    public void NativeArrowReaim_UsesSettledEyeToReachTheRecordedBarrel(float eyeHeight)
    {
        var position = new Vec3(498.091949f, 721.966248f, 21.6503487f);
        var target = new Vec3(498.269f, 721.444f, 22.0302753f);
        var eye = position + (Vec3.Up * eyeHeight);
        var direction = SiegeInteractionDebugBehavior.GetNativeStagingDirection(position, eyeHeight, target);

        Assert.True(direction.y < 0f);
        Assert.True((eye + (direction * (target - eye).Length) - target).Length < 0.0001f);
        var oldCamera = new Vec3(498.1203f, 722.0794f, 25.4501629f);
        var oldRay = new Vec3(0.0298132747f, 0.119006753f, -0.9924458f);
        var oldHit = oldCamera + (oldRay * ((target.z - oldCamera.z) / oldRay.z));
        Assert.True((oldHit - target).Length > 1f);
    }

    [Theory]
    [InlineData(0.966359735f, 17, true, true)]
    [InlineData(0.1f, 17, true, true)]
    [InlineData(0.8f, 791, true, false)]
    [InlineData(0.8f, 0, true, false)]
    [InlineData(0.8f, 17, false, false)]
    [InlineData(float.NaN, 17, true, false)]
    [InlineData(float.PositiveInfinity, 17, true, false)]
    [InlineData(-0.1f, 17, true, false)]
    [InlineData(2f, 17, true, false)]
    public void ArrowReaim_BindsCloseCameraToCurrentSurfaceWithoutMovingActorAndRestoresCamera(
        float collisionDistance, int entityId, bool rayHit, bool accepted)
    {
        using var mission = new MissionCurrentScope();
        var harmony = new Harmony("coop.tests.arrow-eye-camera");
        var managedInterface = AccessTools.Field(AccessTools.TypeByName("TaleWorlds.DotNet.LibraryApplicationInterface"), "IManaged");
        var originalManagedInterface = managedInterface.GetValue(null);
        try
        {
            managedInterface.SetValue(null, typeof(System.Reflection.DispatchProxy).GetMethod("Create", Type.EmptyTypes)
                .MakeGenericMethod(managedInterface.FieldType, typeof(ArrowManagedBridge)).Invoke(null, null));
            void Patch(System.Reflection.MethodBase method, string prefix) => harmony.Patch(method,
                prefix: new HarmonyMethod(AccessTools.Method(typeof(SiegeInteractionDebugBehaviorTests), prefix)));
            Patch(AccessTools.Method(typeof(ScriptComponentBehavior), "CacheEditableFieldsForAllScriptComponents"), nameof(SkipScriptComponentCache));
            Patch(AccessTools.Method(typeof(Agent), nameof(Agent.IsActive)), nameof(ObservedAgentActive));
            Patch(AccessTools.PropertyGetter(typeof(Agent), nameof(Agent.IsUsingGameObject)), nameof(ObservedAgentUnused));
            Patch(AccessTools.PropertyGetter(typeof(Agent), nameof(Agent.MountAgent)), nameof(SkipScriptComponentCache));
            Patch(AccessTools.PropertyGetter(typeof(Agent), nameof(Agent.Position)), nameof(ArrowActorPosition));
            Patch(AccessTools.PropertyGetter(typeof(Agent), nameof(Agent.AgentScale)), nameof(ArrowActorScale));
            Patch(AccessTools.PropertyGetter(typeof(Agent), nameof(Agent.Monster)), nameof(ArrowActorMonster));
            Patch(AccessTools.PropertySetter(typeof(Agent), nameof(Agent.LookDirection)), nameof(SkipScriptComponentCache));
            Patch(AccessTools.Method(typeof(Agent), nameof(Agent.TeleportToPosition)), nameof(ArrowTeleport));
            Patch(AccessTools.Method(typeof(Agent), nameof(Agent.GetOffhandWieldedItemIndex)), nameof(ArrowEmptyHand));
            Patch(AccessTools.Method(typeof(Agent), nameof(Agent.GetPrimaryWieldedItemIndex)), nameof(ArrowEmptyHand));
            Patch(AccessTools.Method(typeof(Agent), nameof(Agent.TryToWieldWeaponInSlot)), nameof(SkipScriptComponentCache));
            Patch(AccessTools.Method(typeof(StandingPoint), nameof(StandingPoint.GetUserFrameForAgent)), nameof(ArrowUserFrame));
            Patch(AccessTools.Method(typeof(WorldPosition), nameof(WorldPosition.GetGroundVec3)), nameof(ArrowActorPosition));
            Patch(AccessTools.Method(typeof(WeakGameEntity), nameof(WeakGameEntity.ComputeGlobalPhysicsBoundingBoxCenter)), nameof(ArrowPhysicsTarget));
            Patch(AccessTools.PropertyGetter(typeof(WeakGameEntity), nameof(WeakGameEntity.GlobalPosition)), nameof(ArrowPhysicsTarget));
            Patch(AccessTools.PropertyGetter(typeof(Mission), nameof(Mission.Scene)), nameof(ArrowMissionScene));
            Patch(AccessTools.Method(typeof(Scene), nameof(Scene.FocusRayCastForFixedPhysics)), nameof(ArrowSurfaceRay));
            Patch(AccessTools.Method(typeof(Camera), nameof(Camera.CreateCamera)), nameof(ArrowCreateCamera));
            Patch(AccessTools.Method(typeof(Camera), nameof(Camera.FillParametersFrom)), nameof(SkipScriptComponentCache));
            Patch(AccessTools.Method(typeof(Camera), nameof(Camera.LookAt)), nameof(ArrowCameraLookAt));
            Patch(AccessTools.Method(typeof(Camera), nameof(Camera.SetFovVertical)), nameof(SkipScriptComponentCache));
            Patch(AccessTools.Method(typeof(Camera), nameof(Camera.ReleaseCamera)), nameof(ArrowReleaseCamera));
            Patch(AccessTools.PropertyGetter(typeof(TaleWorlds.Engine.Screen), nameof(TaleWorlds.Engine.Screen.AspectRatio)), nameof(ArrowActorScale));
#pragma warning disable SYSLIB0050
            var screenType = AccessTools.TypeByName("TaleWorlds.MountAndBlade.View.Screens.MissionScreen");
            var screen = FormatterServices.GetUninitializedObject(screenType);
            var agent = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
            arrowMonster = (Monster)FormatterServices.GetUninitializedObject(typeof(Monster));
            var barrel = (ArrowBarrel)FormatterServices.GetUninitializedObject(typeof(ArrowBarrel));
            var point = (StandingPoint)FormatterServices.GetUninitializedObject(typeof(StandingPoint));
            arrowScene = (Scene)FormatterServices.GetUninitializedObject(typeof(Scene));
#pragma warning restore SYSLIB0050
            GC.SuppressFinalize(arrowScene);
            var entityConstructor = AccessTools.Constructor(typeof(WeakGameEntity), new[] { typeof(UIntPtr) });
            AccessTools.Field(typeof(ScriptComponentBehavior), "_gameEntity").SetValue(barrel,
                entityConstructor.Invoke(new object[] { new UIntPtr(17u) }));
            arrowSurfaceEntity = (WeakGameEntity)entityConstructor.Invoke(new object[] { new UIntPtr((uint)entityId) });
            arrowSurfaceDistance = collisionDistance;
            arrowSurfaceHit = rayHit;
            AccessTools.Property(typeof(Monster), nameof(Monster.StandingEyeHeight)).SetValue(arrowMonster, 1.4f);
            AccessTools.Property(typeof(Agent), nameof(Agent.Equipment)).SetValue(agent, new MissionEquipment());
            var bow = new ItemObject("test_bow");
            bow.AddWeapon(new WeaponComponentData(null, WeaponClass.Bow, default), null);
            agent.Equipment[EquipmentIndex.Weapon0] = new MissionWeapon(bow, null, null, 1);
            AccessTools.Property(typeof(MissionObject), nameof(MissionObject.Id)).SetValue(barrel, new MissionObjectId(17, false));
            AccessTools.Property(typeof(UsableMachine), nameof(UsableMachine.StandingPoints)).SetValue(barrel, new MBList<StandingPoint> { point });
            ((MBList<MissionObject>)AccessTools.Field(typeof(Mission), "_missionObjects").GetValue(mission.Instance)).Add(barrel);
            var behavior = new SiegeInteractionDebugBehavior(Mock.Of<IMessageBroker>());
            AccessTools.Property(typeof(MissionBehavior), "Mission").SetValue(behavior, mission.Instance);
            void Set(string field, object value) => AccessTools.Field(typeof(SiegeInteractionDebugBehavior), field).SetValue(behavior, value);
            object Get(string field) => AccessTools.Field(typeof(SiegeInteractionDebugBehavior), field).GetValue(behavior);
            object GetCamera() => AccessTools.Property(screenType, "CustomCamera").GetValue(screen);
            Set("capturedAgent", agent);
            Set("capturedScreen", screen);
            Set("previousMainHand", EquipmentIndex.None);
            Set("capturedPosition", new Vec3(434f, 690f, 11f));
            Set("capturedCameraBearing", 0.25f);
            Set("capturedCameraElevation", -0.1f);
            arrowPosition = new Vec3(498.091949f, 721.966248f, 21.6503487f);
            arrowTeleports = arrowCameraReleases = 0;

            var stage = AccessTools.Method(typeof(SiegeInteractionDebugBehavior), "Stage");
            stage.Invoke(behavior, new object[] { screen, agent, 17, 0, false, true, false });
            Assert.Null(GetCamera());
            Assert.True((bool)Get("nativeCameraStaged"));
            Assert.Equal(1, arrowTeleports);
            stage.Invoke(behavior, new object[] { screen, agent, 17, 0, false, true, true });
            Assert.Equal(1, arrowTeleports);
            if (accepted)
            {
                Assert.Same(Get("stagingCamera"), GetCamera());
                Assert.NotNull(GetCamera());
                Assert.Equal(new Vec3(498.269f, 721.444f, 22.0302753f), arrowCameraTarget);
                var direction = (arrowCameraTarget - arrowCameraEye).NormalizedCopy();
                var origin = arrowCameraEye + (direction * arrowCameraEye.AsVec2.Distance(arrowPosition.AsVec2));
                var surface = arrowPosition + (Vec3.Up * 1.6f) + (direction * collisionDistance);
                Assert.True(Math.Abs((surface - origin).Length - Math.Min(collisionDistance, 0.3f)) < 0.0002f);
                if (collisionDistance > 0.3f) Assert.True((surface - origin).Length > 0.2f);
                Assert.True((surface - origin).Length < 0.4f);
                Assert.True((arrowCameraEye - (arrowPosition + (Vec3.Up * 1.6f))).Length < collisionDistance);
                var atTorchFront = arrowCameraEye + (direction * ((721.545654f - arrowCameraEye.y) / direction.y));
                Assert.True(atTorchFront.z + 0.2f < 22.77746f);
                Assert.Equal("fixture_staged_native_focus_pending", Get("status"));
            }
            else
            {
                Assert.Null(GetCamera());
                Assert.Null(Get("stagingCamera"));
                Assert.Equal("fixture_target_unavailable", Get("status"));
            }
            Assert.False((bool)Get("pressInvoked"));
            Assert.False((bool)Get("externalInputArmed"));
            Assert.Null(Get("functionalAction"));

            AccessTools.Method(typeof(SiegeInteractionDebugBehavior), "Restore").Invoke(behavior, new object[] { screen, agent });
            Assert.Equal("fixture_restored", Get("status"));
            Assert.Null(GetCamera());
            Assert.Null(Get("stagingCamera"));
            Assert.False((bool)Get("nativeCameraStaged"));
            Assert.Equal(0.25f, AccessTools.Property(screenType, "CameraBearing").GetValue(screen));
            Assert.Equal(-0.1f, AccessTools.Property(screenType, "CameraElevation").GetValue(screen));
            Assert.Equal(1, arrowCameraReleases);
            Assert.Equal(2, arrowTeleports);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            managedInterface.SetValue(null, originalManagedInterface);
        }
    }

    private static Vec3 arrowPosition, arrowCameraEye, arrowCameraTarget;
    private static Monster arrowMonster;
    private static Scene arrowScene;
    private static WeakGameEntity arrowSurfaceEntity;
    private static float arrowSurfaceDistance;
    private static bool arrowSurfaceHit;
    private static int arrowTeleports, arrowCameraReleases;
    private static bool ArrowMissionScene(ref Scene __result) { __result = arrowScene; return false; }
    private static bool ArrowSurfaceRay(Vec3 __0, Vec3 __1, ref float __2, ref Vec3 __3,
        ref WeakGameEntity __4, float __5, BodyFlags __6, ref bool __result)
    {
        Assert.Equal(arrowPosition + (Vec3.Up * 1.6f), __0);
        Assert.Equal(new Vec3(498.269f, 721.444f, 22.0302753f), __1);
        Assert.Equal(0.01f, __5);
        Assert.Equal(unchecked((BodyFlags)(-251707585)), __6);
        __2 = arrowSurfaceDistance;
        __3 = __0 + ((__1 - __0).NormalizedCopy() * arrowSurfaceDistance);
        __4 = arrowSurfaceEntity;
        __result = arrowSurfaceHit;
        return false;
    }
    private static bool ArrowActorMonster(ref Monster __result) { __result = arrowMonster; return false; }
    private static bool ArrowActorPosition(ref Vec3 __result) { __result = arrowPosition; return false; }
    private static bool ArrowActorScale(ref float __result) { __result = 1f; return false; }
    private static bool ArrowTeleport(Vec3 __0) { arrowPosition = __0; arrowTeleports++; return false; }
    private static bool ArrowEmptyHand(ref EquipmentIndex __result) { __result = EquipmentIndex.None; return false; }
    private static bool ArrowUserFrame(ref WorldFrame __result)
    {
        __result = new WorldFrame(Mat3.Identity, default);
        return false;
    }
    private static bool ArrowPhysicsTarget(ref Vec3 __result) { __result = new Vec3(498.269f, 721.444f, 22.0302753f); return false; }
    private static bool ArrowCreateCamera(ref Camera __result)
    {
#pragma warning disable SYSLIB0050
        __result = (Camera)FormatterServices.GetUninitializedObject(typeof(Camera));
#pragma warning restore SYSLIB0050
        GC.SuppressFinalize(__result);
        AccessTools.Property(typeof(TaleWorlds.DotNet.NativeObject), "Pointer").SetValue(__result, new UIntPtr(91u));
        return false;
    }
    private static bool ArrowCameraLookAt(Vec3 __0, Vec3 __1) { arrowCameraEye = __0; arrowCameraTarget = __1; return false; }
    private static bool ArrowReleaseCamera() { arrowCameraReleases++; return false; }

    public class ArrowManagedBridge : System.Reflection.DispatchProxy
    {
        protected override object Invoke(System.Reflection.MethodInfo method, object[] args)
        {
            if (method.Name == "GetClassTypeDefinitionCount") return 0;
            throw new InvalidOperationException("Unexpected native call: " + method.Name);
        }
    }

    [Theory]
    [InlineData(typeof(StonePile))]
    [InlineData(typeof(ArrowBarrel))]
    [InlineData(typeof(Ballista))]
    [InlineData(typeof(Mangonel))]
    public void NativeTarget_UsesCurrentPhysicsCenterWithoutChangingObserverTarget(Type machineType)
    {
        using var mission = new MissionCurrentScope();
        var harmony = new Harmony("coop.tests.native-physics-target");
        try
        {
            harmony.Patch(AccessTools.Method(typeof(ScriptComponentBehavior), "CacheEditableFieldsForAllScriptComponents"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(SiegeInteractionDebugBehaviorTests),
                    nameof(SkipScriptComponentCache))));
            harmony.Patch(AccessTools.Method(typeof(WeakGameEntity), nameof(WeakGameEntity.ComputeGlobalPhysicsBoundingBoxCenter)),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(SiegeInteractionDebugBehaviorTests),
                    nameof(NativePhysicsCenter))));
#pragma warning disable SYSLIB0050
            var machine = (UsableMachine)FormatterServices.GetUninitializedObject(machineType);
            StandingPoint pilot = null;
            if (machine is RangedSiegeWeapon)
            {
                var body = (SynchedMissionObject)FormatterServices.GetUninitializedObject(typeof(SynchedMissionObject));
                var entity = AccessTools.Constructor(typeof(WeakGameEntity), new[] { typeof(UIntPtr) })
                    .Invoke(new object[] { new UIntPtr(880u) });
                AccessTools.Field(typeof(ScriptComponentBehavior), "_gameEntity").SetValue(body, entity);
                if (machine is Ballista)
                    AccessTools.Property(typeof(Ballista), "ballistaBody").SetValue(machine, body);
                else
                {
                    AccessTools.Field(typeof(Mangonel), "_body").SetValue(machine, body);
                    pilot = (StandingPoint)FormatterServices.GetUninitializedObject(typeof(StandingPoint));
                    AccessTools.Property(typeof(UsableMachine), "PilotStandingPoint").SetValue(machine, pilot);
                }
                harmony.Patch(AccessTools.PropertyGetter(typeof(WeakGameEntity), nameof(WeakGameEntity.Name)),
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(SiegeInteractionDebugBehaviorTests), nameof(BallistaBodyName))));
                foreach (var name in new[] { nameof(WeakGameEntity.GlobalBoxMin), nameof(WeakGameEntity.GlobalBoxMax) })
                    harmony.Patch(AccessTools.PropertyGetter(typeof(WeakGameEntity), name),
                        prefix: new HarmonyMethod(AccessTools.Method(typeof(SiegeInteractionDebugBehaviorTests), nameof(BallistaRenderBounds))));
                harmony.Patch(AccessTools.Method(typeof(SiegeInteractionDebugBehavior), "DescribeAncestors"),
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(SiegeInteractionDebugBehaviorTests), nameof(BallistaAncestors))));
            }
#pragma warning restore SYSLIB0050
            var behavior = new SiegeInteractionDebugBehavior(Mock.Of<IMessageBroker>());
            var standingPosition = new Vec3(498.52f, 720.788f, 36.16533f);

            Assert.Equal(standingPosition, behavior.GetStagingTarget(machine, standingPosition, true, true, pilot));
            Assert.Equal(standingPosition, behavior.GetStagingTarget(machine, standingPosition, false));
            var expectedX = machine is RangedSiegeWeapon ? 500f : 499f;
            Assert.Equal(new Vec3(expectedX, 721f, 36.5f),
                behavior.GetStagingTarget(machine, standingPosition, false, true, pilot));
            if (machine is Mangonel)
            {
                Assert.Equal(new Vec3(expectedX, 721f, 36.5f),
                    behavior.GetStagingTarget(machine, standingPosition, false, standingPoint: pilot));
                Assert.Equal(standingPosition, behavior.GetStagingTarget(machine, standingPosition, true, standingPoint: pilot));
#pragma warning disable SYSLIB0050
                var otherPoint = (StandingPoint)FormatterServices.GetUninitializedObject(typeof(StandingPoint));
                var loadPoint = (StandingPointWithWeaponRequirement)FormatterServices.GetUninitializedObject(typeof(StandingPointWithWeaponRequirement));
#pragma warning restore SYSLIB0050
                AccessTools.Field(typeof(RangedSiegeWeapon), "LoadAmmoStandingPoint").SetValue(machine, loadPoint);
                Assert.Equal(new Vec3(expectedX, 721f, 36.5f),
                    behavior.GetStagingTarget(machine, standingPosition, false, standingPoint: loadPoint));
                Assert.Equal(standingPosition, behavior.GetStagingTarget(machine, standingPosition, true, standingPoint: loadPoint));
                Assert.Equal(standingPosition, behavior.GetStagingTarget(machine, standingPosition, false, standingPoint: otherPoint));
                AccessTools.Field(typeof(Mangonel), "_body").SetValue(machine, null);
                Assert.Throws<InvalidOperationException>(() =>
                    behavior.GetStagingTarget(machine, standingPosition, false, standingPoint: pilot));
                Assert.Throws<InvalidOperationException>(() =>
                    behavior.GetStagingTarget(machine, standingPosition, false, standingPoint: loadPoint));
            }
            var target = JObject.FromObject(AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "nativeAimTarget").GetValue(behavior));
            Assert.Equal(expectedX, target["target"]["x"].Value<float>());
            Assert.Null(AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "capturedAgent").GetValue(behavior));
            Assert.Null(AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "stagingCamera").GetValue(behavior));
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }

    private static bool NativePhysicsCenter(WeakGameEntity __instance, ref Vec3 __result)
    {
        Assert.Contains(__instance.Pointer.ToUInt64(), new ulong[] { 0, 880 });
        __result = new Vec3(__instance.Pointer == UIntPtr.Zero ? 499f : 500f, 721f, 36.5f);
        return false;
    }

    private static bool BallistaBodyName(ref string __result)
    {
        __result = "ballista_body";
        return false;
    }

    private static bool BallistaRenderBounds(System.Reflection.MethodBase __originalMethod, ref Vec3 __result)
    {
        __result = __originalMethod.Name == "get_GlobalBoxMin"
            ? new Vec3(495f, 710f, 36f) : new Vec3(507f, 728f, 39f);
        return false;
    }

    private static bool BallistaAncestors(ref object[] __result)
    {
        __result = Array.Empty<object>();
        return false;
    }

    [Theory]
    [InlineData(false, 1410)]
    [InlineData(true, 743)]
    public void NativeBallistaAim_RejectsUnstagedOrDifferentTargetBeforeNativeReads(bool staged, int stagedMachineId)
    {
        var behavior = new SiegeInteractionDebugBehavior(Mock.Of<IMessageBroker>());
        AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "nativeCameraStaged").SetValue(behavior, staged);
        AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "observedMachineId").SetValue(behavior, stagedMachineId);

        AccessTools.Method(typeof(SiegeInteractionDebugBehavior), "Stage").Invoke(behavior,
            new object[] { null, null, 1410, 0, false, true, true });

        Assert.Equal("fixture_stage_rejected", AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "status").GetValue(behavior));
        Assert.Equal(staged, AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "nativeCameraStaged").GetValue(behavior));
        Assert.Equal(stagedMachineId, AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "observedMachineId").GetValue(behavior));
        Assert.Null(AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "capturedAgent").GetValue(behavior));
        Assert.False((bool)AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "externalInputArmed").GetValue(behavior));
    }

    [Theory]
    [InlineData(0.5f)]
    [InlineData(3f)]
    public void NativeBallistaStaging_AimsAtResolvedBodyInsteadOfPilotFacing(float bodyHeight)
    {
        var userPosition = new Vec3(448.91153f, 690.1686f, 31.337246f);
        var eye = userPosition + (Vec3.Up * 2.2f);
        var target = userPosition + new Vec3(-0.7f, 1.2f, bodyHeight);
        var direction = SiegeInteractionDebugBehavior.GetNativeStagingDirection(userPosition, 2.2f, target);
        var distance = (target - eye).Length;

        Assert.True((eye + (direction * distance) - target).Length < 0.0001f);
        Assert.True(Vec3.DotProduct(direction, new Vec3(0.365059f, -0.918913f, 0f)) < 0f);
        Assert.Equal(bodyHeight > 2.2f, direction.z > 0f);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void NativeBallistaTarget_MissingBodyRejectsOnlyNativeActorStaging(bool nativeCamera, bool watchOnly)
    {
        using var mission = new MissionCurrentScope();
        var harmony = new Harmony("coop.tests.ballista-native-target");
        try
        {
            harmony.Patch(AccessTools.Method(typeof(ScriptComponentBehavior), "CacheEditableFieldsForAllScriptComponents"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(SiegeInteractionDebugBehaviorTests),
                    nameof(SkipScriptComponentCache))));
#pragma warning disable SYSLIB0050
            var ballista = (Ballista)FormatterServices.GetUninitializedObject(typeof(Ballista));
#pragma warning restore SYSLIB0050
            var behavior = new SiegeInteractionDebugBehavior(Mock.Of<IMessageBroker>());
            var standingPosition = new Vec3(448.91153f, 690.1686f, 31.337246f);
            if (nativeCamera && !watchOnly)
            {
                var error = Assert.Throws<InvalidOperationException>(() =>
                    behavior.GetStagingTarget(ballista, standingPosition, watchOnly, nativeCamera));
                Assert.Contains("no resolved body", error.Message);
            }
            else
            {
                Assert.Equal(standingPosition, behavior.GetStagingTarget(ballista, standingPosition, watchOnly, nativeCamera));
            }
            Assert.Null(AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "capturedAgent").GetValue(behavior));
            Assert.Null(AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "stagingCamera").GetValue(behavior));
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    [Fact]
    public void RawInputEvidence_DoesNotSatisfyGameEdgeOrRelease()
    {
        var behavior = new SiegeInteractionDebugBehavior(Mock.Of<IMessageBroker>());
        var before = DateTime.UtcNow;
        behavior.RecordInputSample(false, false, false, new
        {
            isKeysAllowed = false, registeredGameKeyId = 13, registeredKeyboardKey = "F",
            rawPressed = true, rawDown = true, rawReleased = false
        });
        AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "tick").SetValue(behavior, 1);
        behavior.RecordInputSample(false, false, false, new
        {
            isKeysAllowed = false, registeredGameKeyId = 13, registeredKeyboardKey = "F",
            rawPressed = false, rawDown = false, rawReleased = true
        });
        var samples = JArray.FromObject(AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "inputSamples").GetValue(behavior));
        Assert.True(samples[0]["nativeInput"]["rawDown"].Value<bool>());
        Assert.True(samples[1]["nativeInput"]["rawReleased"].Value<bool>());
        Assert.False(samples[0]["nativeInput"]["isKeysAllowed"].Value<bool>());
        Assert.Equal(13, samples[0]["nativeInput"]["registeredGameKeyId"].Value<int>());
        Assert.InRange(samples[0]["recordedUtc"].Value<DateTime>().ToUniversalTime(), before, DateTime.UtcNow);
        Assert.False((bool)AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "edgeObserved").GetValue(behavior));
        Assert.False((bool)AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "edgeCleared").GetValue(behavior));
    }

    [Fact]
    public void InputObservation_WithAllSamplesAndTargetDetailsFitsTheUnchangedWireLimit()
    {
        var behavior = new SiegeInteractionDebugBehavior(Mock.Of<IMessageBroker>());
        for (int frame = 0; frame < 300; frame++)
        {
            AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "tick").SetValue(behavior, frame);
            behavior.RecordInputSample(false, false, false, new
            {
                contextType = "TaleWorlds.InputSystem.InputContext", contextId = 20862267,
                layerType = "TaleWorlds.Engine.Screens.SceneLayer", layerId = 57646574,
                isKeysAllowed = true, registeredGameKeyId = 13, registeredCategory = "ActionCategory",
                registeredKeyboardKey = "F", registeredVirtualKey = 70, armedVirtualKey = 70,
                rawPressed = false, rawDown = false, rawReleased = false
            });
        }
        for (int index = 0; index < 16; index++) behavior.AppendUseDispatch(new
        {
            requestId = "ballista-testclient2-use", call = index, method = "HandleStartUsingAction", phase = "entry",
            tick = 830, recordedUtc = DateTime.UtcNow.ToString("O"), sessionId = "MapEvent_Created_1",
            controllerId = "testclient2", actorId = Guid.NewGuid().ToString("N"),
            originalOwner = "testclient2", currentAuthority = "testclient2", machineId = 884, pointId = 882,
            pointUserId = Guid.NewGuid().ToString("N"), pointHasUser = true, pointHasAIUser = true,
            pointUserIsActor = false, sameFocusAgent = true, nativeClient = false, nativeClientOrReplay = false,
            radialMenuActive = false, itemInteractionEnabled = true, orderMenuOpen = false, ableToUseMachine = true,
            pressed = true, down = true, released = false, focusedObject = new { type = "StandingPoint", id = 882 },
            interactableObject = new { type = "StandingPoint", id = 882 },
            argumentObject = new { type = "StandingPoint", id = 882 }, usingObject = true,
            usedObject = new { type = "StandingPoint", id = 882 }, exception = "System.InvalidOperationException",
            stop = new { isSuccessful = false, flags = int.MaxValue,
                callers = index == 0 ? Enumerable.Repeat(new string('c', 256), 8).ToArray() : null }
        });
        var samples = AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "inputSamples").GetValue(behavior);
        var point = new { x = 498.52f, y = 720.788f, z = 35.835f };
        var ancestors = Enumerable.Range(0, 16).Select(depth =>
        {
            var ancestor = new
            {
                depth, entityPointer = "00000123456789AB",
                id = depth == 0 ? (int?)null : 743, type = depth == 0 ? null : "DestructableComponent",
                focus = depth == 0 ? null : new { type = "DestructableComponent", id = 743 },
                isFocusable = depth == 0 ? (bool?)null : true
            };
            return depth == 0 ? new
            {
                ancestor.depth, ancestor.entityPointer, name = new string('e', 256), bodyFlags = 79617,
                min = point, max = new { x = 499.52f, y = 721.788f, z = 36.835f },
                ancestor.id, ancestor.type, ancestor.focus, ancestor.isFocusable
            } : (object)ancestor;
        }).ToArray();
        var observation = new
        {
            observedMachineId = 142, inputSamples = samples, useDispatch = behavior.ReadUseDispatch(),
            mainAgentId = Guid.NewGuid().ToString("N"),
            observedAgent = new
            {
                agentId = Guid.NewGuid().ToString("N"), available = true,
                originalOwner = "testclient2", currentAuthority = "testclient2", tick = 300,
                recordedUtc = DateTime.UtcNow, usingObject = true, usedObject = new { id = 1408, type = "StandingPoint" },
                actions = new[] { new { channel = 0, index = 101, name = "act_use_ballista", type = "act_none" },
                    new { channel = 1, index = -1, name = "act_none", type = "act_none" } }
            },
            observerFrame = new
            {
                agentId = Guid.NewGuid().ToString("N"), tick = 1642,
                rejectionReason = "horizontal_look_too_short", visualEntityAvailable = true,
                lookDirection = SiegeInteractionDebugBehavior.DescribePosition(new Vec3(0.3857944f, -0.255507f, -0.8864982f)),
                horizontalLookLengthSquared = 0.2141193f
            },
            nativeAimTarget = new
            {
                requestId = "ballista-testclient2-stage", tick = 685, recordedUtc = DateTime.UtcNow,
                machineId = 1410, bodyId = 1409, bodyName = "ballista_body", bodyTag = "BallistaBody",
                min = new { x = 447f, y = 689f, z = 31f }, max = new { x = 450f, y = 692f, z = 34f },
                target = new { x = 448.5f, y = 690.5f, z = 32.5f },
                ancestors
            },
            focusDiagnostic = new
            {
                fallbackProbes = new
                {
                    length = 10f, nearHit = true, nearDistance = 10f, nearPoint = point, nearAncestors = ancestors,
                    wideHit = true, wideDistance = 10f, widePoint = point, wideAncestors = ancestors
                },
                rayProbe = new
                {
                    length = 10f, blockerHit = true, blockerDistance = 10f, blockerPoint = point,
                    blockerAncestors = ancestors,
                    terrainHit = true, terrainDistance = 10f, terrainPoint = point, terrainAncestors = ancestors,
                    focusHit = true, focusDistance = 10f, focusPoint = point, focusAncestors = ancestors
                }
            },
            machines = new[] { new
            {
                id = 142, type = "StonePile", IsDeactivated = false, IsDisabled = false,
                gateState = (int?)null, stoneAmmo = 12, stoneItemId = "boulder",
                hitPoints = (float?)null, ladderState = (int?)null, rangedState = (int?)null,
                authority = new
                {
                    available = true, machineId = 142, sessionId = "MapEvent_Created_1",
                    observerControllerId = "testclient2", hostControllerId = "testclient", hostEpoch = 1,
                    authorityEpoch = 1, authorityRevision = 2, authorityKnown = true,
                    simulator = "testclient2", claimed = true, simulatedLocally = true,
                    localUser = true, localMover = true, contested = true, unusedSeconds = 1.25f,
                    releaseAfterSeconds = 2f, graceSeconds = 4f, claimRetrySeconds = 0.75f
                },
                ammoSupply = new
                {
                    weaponId = 1554, itemId = "mangonel_boulder", loadPointId = 1546, loadPointIndex = 2,
                    pickupPoints = Enumerable.Range(0, 64).Select(index => new
                    {
                        pointId = 1556 + index, machineId = 1564, pointIndex = index, active = true, vacant = true
                    }).ToArray()
                },
                standingPoints = Enumerable.Range(0, 64).Select(index => new
                {
                    index, id = 141 + index, IsDeactivated = false, IsDisabledForPlayers = false,
                    disabledForMainAgent = false, occupied = false, HasAIUser = false,
                    vacantForPlayer = true, ownedByMainAgent = false, distanceSquared = 0f,
                    heightDifference = 0f, reachable = true, heightWithinReach = true,
                    x = 498.52f, y = 720.788f, z = 35.835f
                }).ToArray()
            } }
        };
        string output = "LIVE_TEST_JSON=" + JsonConvert.SerializeObject(observation);
        using var document = System.Text.Json.JsonDocument.Parse(output.Substring("LIVE_TEST_JSON=".Length));
        var response = LiveTestResponse.Success("poll-testclient2", new LiveTestProcessInfo
        {
            Pid = 20800, Role = "client", PlatformId = "testclient2",
            RunToken = "c54012d8ff9f4a76a78699980cdaa9b8", ProcessStartedUtc = DateTime.UtcNow
        }, new { output, hasStructuredResult = true, structuredResult = document.RootElement.Clone() });

        string wire = LiveTestProtocol.SerializeResponse(response);

        int wireBytes = System.Text.Encoding.UTF8.GetByteCount(wire);
        Assert.True(wireBytes < LiveTestProtocol.MaximumMessageBytes / 2,
            $"Payload uses {wireBytes} UTF-8 bytes; expected less than {LiveTestProtocol.MaximumMessageBytes / 2}.");
        Assert.True(LiveTestProtocol.TryDeserializeResponse(wire, out var actual, out var error));
        Assert.Null(error);
        var result = Assert.IsType<System.Text.Json.JsonElement>(actual.Result);
        Assert.Equal(output, result.GetProperty("output").GetString());
        Assert.Equal(document.RootElement.GetRawText(), result.GetProperty("structuredResult").GetRawText());
        Assert.Equal(300, result.GetProperty("structuredResult").GetProperty("inputSamples").GetArrayLength());
        var dispatchSamples = result.GetProperty("structuredResult").GetProperty("useDispatch").GetProperty("samples");
        Assert.Equal(16, dispatchSamples.GetArrayLength());
        Assert.Single(dispatchSamples.EnumerateArray().Where(sample =>
            sample.GetProperty("stop").GetProperty("callers").ValueKind != System.Text.Json.JsonValueKind.Null));
        Assert.Equal(8, dispatchSamples[0].GetProperty("stop").GetProperty("callers").GetArrayLength());
        Assert.All(dispatchSamples.EnumerateArray(), sample =>
            Assert.Equal(int.MaxValue, sample.GetProperty("stop").GetProperty("flags").GetInt32()));
        Assert.Equal(64, result.GetProperty("structuredResult").GetProperty("machines")[0]
            .GetProperty("standingPoints").GetArrayLength());
        var anonymousHit = result.GetProperty("structuredResult").GetProperty("focusDiagnostic")
            .GetProperty("rayProbe").GetProperty("focusAncestors")[0];
        Assert.Equal(System.Text.Json.JsonValueKind.Null, anonymousHit.GetProperty("id").ValueKind);
        Assert.Equal("00000123456789AB", anonymousHit.GetProperty("entityPointer").GetString());
        Assert.Equal(3, anonymousHit.GetProperty("min").EnumerateObject().Count());
        var parentHit = result.GetProperty("structuredResult").GetProperty("focusDiagnostic")
            .GetProperty("rayProbe").GetProperty("focusAncestors")[1];
        Assert.Equal("00000123456789AB", parentHit.GetProperty("entityPointer").GetString());
        Assert.False(parentHit.TryGetProperty("name", out _));
        Assert.False(parentHit.TryGetProperty("bodyFlags", out _));
        Assert.False(parentHit.TryGetProperty("min", out _));
        Assert.False(parentHit.TryGetProperty("max", out _));
    }

    [Fact]
    public void ExternalInputObservation_MissedEdgeAllowsOnlyReleasedKeyCleanupStop()
    {
        var behavior = new SiegeInteractionDebugBehavior(Mock.Of<IMessageBroker>());
        AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "externalInputArmed").SetValue(behavior, true);
        behavior.RecordInputSample(false, false, false);
        Assert.True(behavior.CanAcceptInputAction("arm-stop", false, false));
        Assert.False(behavior.CanAcceptInputAction("arm-stop", true, false));
        Assert.False(behavior.CanAcceptInputAction("arm-stop", false, true));
        Assert.False(behavior.CanAcceptInputAction("arm-use", false, false));
        Assert.False(behavior.CanAcceptInputAction("approach", false, false));
        Assert.True(behavior.CanAcceptInputAction("restore", false, false));
        Assert.False((bool)AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "edgeCleared").GetValue(behavior));
    }

    [Fact]
    public void ExternalInputObservation_RequiresAnObservedEdgeAndLaterReleaseWithoutClaimingInjection()
    {
        var behavior = new SiegeInteractionDebugBehavior(Mock.Of<IMessageBroker>());
        AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "externalInputArmed").SetValue(behavior, true);
        behavior.RecordInputSample(false, false, false);
        Assert.False((bool)AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "edgeObserved").GetValue(behavior));
        Assert.False((bool)AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "edgeCleared").GetValue(behavior));
        behavior.RecordInputSample(true, true, false);
        Assert.True((bool)AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "edgeObserved").GetValue(behavior));
        Assert.False((bool)AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "edgeCleared").GetValue(behavior));
        AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "tick").SetValue(behavior, 1);
        behavior.RecordInputSample(false, false, true);
        Assert.True((bool)AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "edgeCleared").GetValue(behavior));
        Assert.False((bool)AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "pressInvoked").GetValue(behavior));
    }

    [Theory]
    [InlineData("already_captured")]
    [InlineData("combat_camera_missing")]
    [InlineData("agent_missing")]
    [InlineData("agent_inactive")]
    [InlineData("agent_using_object")]
    [InlineData("agent_mounted")]
    [InlineData("battle_end_not_ready")]
    public void CaptureRejection_RecordsReasonWithoutChangingFixtureState(string reason)
    {
        var behavior = new SiegeInteractionDebugBehavior(Mock.Of<IMessageBroker>());
        Assert.True(behavior.RejectCapture(true, reason));

        Assert.Equal("fixture_capture_rejected",
            AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "status").GetValue(behavior));
        Assert.Equal(reason,
            AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "captureFailureReason").GetValue(behavior));
        Assert.Null(AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "capturedAgent").GetValue(behavior));
        Assert.False((bool)AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "fixtureRestored").GetValue(behavior));
    }

    [Theory]
    [InlineData("_isEnemySideRetreating")]
    [InlineData("_isEnemySideDepleted")]
    [InlineData("_isPlayerSideRetreating")]
    [InlineData("_isPlayerSideDepleted")]
    [InlineData("_isEnemyDefenderPulledBack")]
    public void BattleEndHold_RejectsLatchedOutcomeWithoutClearingIt(string flag)
    {
        using var mission = new MissionCurrentScope();
        var behavior = new SiegeInteractionDebugBehavior(Mock.Of<IMessageBroker>());
        var logic = new BattleEndLogic();
        AccessTools.Property(typeof(MissionBehavior), "Mission").SetValue(behavior, mission.Instance);
        AccessTools.Property(typeof(MissionBehavior), "Mission").SetValue(logic, mission.Instance);
        AccessTools.Field(typeof(BattleEndLogic), flag).SetValue(logic, true);

        Assert.False(behavior.HoldBattleEnd(logic, true));
        Assert.True((bool)AccessTools.Field(typeof(BattleEndLogic), flag).GetValue(logic));
        Assert.True((bool)AccessTools.Field(typeof(BattleEndLogic), "_canCheckForEndCondition").GetValue(logic));
        Assert.Null(AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "capturedBattleEndLogic").GetValue(behavior));
    }

    [Fact]
    public void BattleEndHold_RejectsMissingForeignOrStillDeployingMission()
    {
        using var mission = new MissionCurrentScope();
        var behavior = new SiegeInteractionDebugBehavior(Mock.Of<IMessageBroker>());
        var logic = new BattleEndLogic();
        AccessTools.Property(typeof(MissionBehavior), "Mission").SetValue(behavior, mission.Instance);
        Assert.False(behavior.HoldBattleEnd(null, true));
        Assert.False(behavior.HoldBattleEnd(logic, true));
        AccessTools.Property(typeof(MissionBehavior), "Mission").SetValue(logic, mission.Instance);
        Assert.False(behavior.HoldBattleEnd(logic, false));
        logic.ChangeCanCheckForEndCondition(false);
        Assert.False(behavior.HoldBattleEnd(logic, true));
        logic.ChangeCanCheckForEndCondition(true);
        using (var otherMission = new MissionCurrentScope())
            Assert.False(behavior.HoldBattleEnd(logic, true));
        Assert.True((bool)AccessTools.Field(typeof(BattleEndLogic), "_canCheckForEndCondition").GetValue(logic));
    }

    [Fact]
    public void BattleEndHold_StopsNativeOutcomeChecksAcrossRecaptureAndRestoresOnlyItsSwitchOnce()
    {
        using var mission = new MissionCurrentScope();
        var behavior = new SiegeInteractionDebugBehavior(Mock.Of<IMessageBroker>());
        var logic = new BattleEndLogic();
        AccessTools.Property(typeof(MissionBehavior), "Mission").SetValue(behavior, mission.Instance);
        AccessTools.Property(typeof(MissionBehavior), "Mission").SetValue(logic, mission.Instance);
        AccessTools.Field(typeof(BattleEndLogic), "_canCheckForEndConditionSiege").SetValue(logic, true);
        Assert.True(behavior.CanHoldBattleEnd(logic, true));
        Assert.True(behavior.HoldBattleEnd(logic, true));
        Assert.False((bool)AccessTools.Field(typeof(BattleEndLogic), "_canCheckForEndCondition").GetValue(logic));
        AccessTools.Method(typeof(BattleEndLogic), "CheckIsEnemySideRetreatingOrOneSideDepleted").Invoke(logic, null);
        MissionResult result = null;
        Assert.False(logic.MissionEnded(ref result));
        Assert.Null(result);

        // Pose restoration clears its actor; the battle hold must outlive both players' pose restores.
        AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "capturedAgent").SetValue(behavior, null);
        Assert.True(behavior.HoldBattleEnd(logic, true));
        var replacement = new BattleEndLogic();
        AccessTools.Property(typeof(MissionBehavior), "Mission").SetValue(replacement, mission.Instance);
        Assert.False(behavior.HoldBattleEnd(replacement, true));
        replacement.ChangeCanCheckForEndCondition(false);
        var harmony = new Harmony("coop.tests.battle-end-hold");
        try
        {
            harmony.Patch(AccessTools.Method(typeof(SiegeInteractionDebugBehavior), "ReleaseCamera"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(SiegeInteractionDebugBehaviorTests), nameof(SkipScriptComponentCache))));
            behavior.OnRemoveBehavior();
            Assert.True((bool)AccessTools.Field(typeof(BattleEndLogic), "_canCheckForEndCondition").GetValue(logic));
            Assert.False((bool)AccessTools.Field(typeof(BattleEndLogic), "_canCheckForEndCondition").GetValue(replacement));
            Assert.False(behavior.HoldBattleEnd(logic, true));
            logic.ChangeCanCheckForEndCondition(false);
            behavior.OnRemoveBehavior();
            Assert.False((bool)AccessTools.Field(typeof(BattleEndLogic), "_canCheckForEndCondition").GetValue(logic));
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }

    [Fact]
    public void DismountWithoutAgent_RejectsWithoutCapturingFixture()
    {
        var behavior = new SiegeInteractionDebugBehavior(Mock.Of<IMessageBroker>());
        AccessTools.Method(typeof(SiegeInteractionDebugBehavior), "Dismount")
            .Invoke(behavior, new object[] { null });

        Assert.Equal("fixture_dismount_rejected",
            AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "status").GetValue(behavior));
        Assert.Null(AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "dismountAgent").GetValue(behavior));
        Assert.Null(AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "capturedAgent").GetValue(behavior));
    }

    [Fact]
    public void FocusDiagnosticsWithoutStaging_ReportsMissingContextWithoutCapturingFixture()
    {
        var behavior = new SiegeInteractionDebugBehavior(Mock.Of<IMessageBroker>());
        var result = AccessTools.Method(typeof(SiegeInteractionDebugBehavior), "ReadFocusDiagnostic")
            .Invoke(behavior, new object[] { null, null });
        Assert.Equal("camera_agent_or_scene_missing",
            result.GetType().GetProperty("unavailable").GetValue(result));
        Assert.Null(AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "capturedAgent").GetValue(behavior));
        Assert.Null(AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "stagingCamera").GetValue(behavior));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void StagingTarget_UsesGatePhysicsOnlyForTheInteractingPlayer(bool gate, bool watchOnly)
    {
        using var mission = new MissionCurrentScope();
        var harmony = new Harmony("coop.tests.siege-staging-target");
        try
        {
            harmony.Patch(AccessTools.Method(typeof(ScriptComponentBehavior), "CacheEditableFieldsForAllScriptComponents"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(SiegeInteractionDebugBehaviorTests),
                    nameof(SkipScriptComponentCache))));
            harmony.Patch(AccessTools.Method(typeof(CastleGate), "ComputeGlobalPhysicsBoundingBoxMinMax"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(SiegeInteractionDebugBehaviorTests),
                    nameof(GatePhysicsBounds))));
#pragma warning disable SYSLIB0050
            var machine = (UsableMachine)FormatterServices.GetUninitializedObject(
                gate ? typeof(CastleGate) : typeof(BatteringRam));
#pragma warning restore SYSLIB0050
            if (gate) AccessTools.Field(typeof(CastleGate), "<State>k__BackingField")
                .SetValue(machine, CastleGate.GateState.Closed);
            var behavior = new SiegeInteractionDebugBehavior(Mock.Of<IMessageBroker>());
            var standingPosition = new Vec3(610.707764f, 625.542664f, 60.684f);
            var target = behavior.GetStagingTarget(machine, standingPosition, watchOnly);
            if (gate && !watchOnly)
            {
                Assert.Equal(new Vec3(613f, 625f, 62f), target);
                var eye = standingPosition + (Vec3.Up * 1.6f);
                Assert.True((target - eye).AsVec2.Length > 2f);
            }
            else
            {
                Assert.Equal(standingPosition, target);
            }

            var nativeTarget = behavior.GetStagingTarget(machine, standingPosition, watchOnly, true);
            if (gate && !watchOnly)
            {
                Assert.Equal(new Vec3(613f, 625f, 62f), nativeTarget);
                var nativeAimTarget = JObject.FromObject(
                    AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "nativeAimTarget").GetValue(behavior));
                Assert.Equal(613f, nativeAimTarget["target"]["x"].Value<float>());
                Assert.Equal(625f, nativeAimTarget["target"]["y"].Value<float>());
                Assert.Equal(62f, nativeAimTarget["target"]["z"].Value<float>());
            }
            else
            {
                Assert.Equal(standingPosition, nativeTarget);
                Assert.Null(AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "nativeAimTarget").GetValue(behavior));
            }
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    private static bool SkipScriptComponentCache() => false;

    private static int openGateBodyMode;

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void OpenGateTarget_UsesCurrentEnabledLeafAndRejectsMissingBodies(int mode)
    {
        using var mission = new MissionCurrentScope();
        var harmony = new Harmony("coop.tests.open-gate-native-target");
        try
        {
            harmony.Patch(AccessTools.Method(typeof(ScriptComponentBehavior), "CacheEditableFieldsForAllScriptComponents"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(SiegeInteractionDebugBehaviorTests),
                    nameof(SkipScriptComponentCache))));
            harmony.Patch(AccessTools.Method(typeof(CastleGate), "ComputeGlobalPhysicsBoundingBoxMinMax"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(SiegeInteractionDebugBehaviorTests),
                    nameof(GatePhysicsBounds))));
            harmony.Patch(AccessTools.Method(typeof(WeakGameEntity), nameof(WeakGameEntity.ComputeGlobalPhysicsBoundingBoxCenter)),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(SiegeInteractionDebugBehaviorTests),
                    nameof(OpenGatePhysicsCenter))));
            harmony.Patch(AccessTools.PropertyGetter(typeof(WeakGameEntity), nameof(WeakGameEntity.BodyFlag)),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(SiegeInteractionDebugBehaviorTests), nameof(OpenGateBodyFlags))));
#pragma warning disable SYSLIB0050
            var gate = (CastleGate)FormatterServices.GetUninitializedObject(typeof(CastleGate));
#pragma warning restore SYSLIB0050
            openGateBodyMode = mode;
            AccessTools.Field(typeof(CastleGate), "<State>k__BackingField")
                .SetValue(gate, CastleGate.GateState.Open);
            var bodies = mode == 3 ? Array.Empty<WeakGameEntity>() : new[] { 881u, 882u }
                .Select(pointer => (WeakGameEntity)AccessTools.Constructor(typeof(WeakGameEntity), new[] { typeof(UIntPtr) })
                    .Invoke(new object[] { new UIntPtr(pointer) })).ToArray();

            var behavior = new SiegeInteractionDebugBehavior(Mock.Of<IMessageBroker>());
            var standingPosition = new Vec3(610.707764f, 625.542664f, 60.684f);
            Assert.Equal(standingPosition, behavior.GetStagingTarget(gate, standingPosition, true));
            if (mode == 3)
            {
                Assert.Throws<InvalidOperationException>(() => behavior.GetOpenGateTarget(gate, bodies, standingPosition));
                return;
            }
            var target = behavior.GetOpenGateTarget(gate, bodies, standingPosition);
            var expected = mode == 0 ? new Vec3(611f, 626f, 63f) : new Vec3(614f, 626f, 63f);
            Assert.Equal(expected, target);
            var nativeAimTarget = JObject.FromObject(
                AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "nativeAimTarget").GetValue(behavior));
            Assert.Equal(expected.x, nativeAimTarget["target"]["x"].Value<float>());
            Assert.Equal(expected.y, nativeAimTarget["target"]["y"].Value<float>());
            Assert.Equal(expected.z, nativeAimTarget["target"]["z"].Value<float>());
            Assert.Equal((mode == 0 ? 881UL : 882UL).ToString("X16"), nativeAimTarget["bodyPointer"].Value<string>());
            Assert.Null(AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "capturedAgent").GetValue(behavior));
            Assert.Null(AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "stagingCamera").GetValue(behavior));
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    private static bool OpenGateBodyFlags(WeakGameEntity __instance, ref BodyFlags __result)
    {
        __result = openGateBodyMode == 1 && __instance.Pointer.ToUInt64() == 881UL ? BodyFlags.Disabled : (BodyFlags)0;
        return false;
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    [InlineData(true, 3)]
    public void LadderTarget_BindsActionBodyAndRejectsUnavailableTarget(bool fork, int mode)
    {
        using var mission = new MissionCurrentScope();
        var harmony = new Harmony("coop.tests.ladder-target");
        try
        {
            harmony.Patch(AccessTools.Method(typeof(ScriptComponentBehavior), "CacheEditableFieldsForAllScriptComponents"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(SiegeInteractionDebugBehaviorTests), nameof(SkipScriptComponentCache))));
            harmony.Patch(AccessTools.Method(typeof(WeakGameEntity), nameof(WeakGameEntity.ComputeGlobalPhysicsBoundingBoxCenter)),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(SiegeInteractionDebugBehaviorTests), nameof(OpenGatePhysicsCenter))));
            harmony.Patch(AccessTools.PropertyGetter(typeof(WeakGameEntity), nameof(WeakGameEntity.BodyFlag)),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(SiegeInteractionDebugBehaviorTests), nameof(OpenGateBodyFlags))));
#pragma warning disable SYSLIB0050
            var ladder = (SiegeLadder)FormatterServices.GetUninitializedObject(typeof(SiegeLadder));
            var pickup = (StandingPointWithWeaponRequirement)FormatterServices.GetUninitializedObject(typeof(StandingPointWithWeaponRequirement));
            var lift = (StandingPoint)FormatterServices.GetUninitializedObject(typeof(StandingPoint));
            AccessTools.Property(typeof(UsableMachine), nameof(UsableMachine.StandingPoints))
                .SetValue(ladder, new MBList<StandingPoint> { pickup, lift });
            AccessTools.Field(typeof(SiegeLadder), "_forkPickUpStandingPoint").SetValue(ladder, pickup);
            foreach (var entry in new[] { ("_forkEntity", 881u), ("_ladderBodyObject", 882u) })
            {
                var body = (SynchedMissionObject)FormatterServices.GetUninitializedObject(typeof(SynchedMissionObject));
                var entity = AccessTools.Constructor(typeof(WeakGameEntity), new[] { typeof(UIntPtr) })
                    .Invoke(new object[] { new UIntPtr(entry.Item2) });
                AccessTools.Field(typeof(ScriptComponentBehavior), "_gameEntity").SetValue(body, entity);
                AccessTools.Field(typeof(SiegeLadder), entry.Item1).SetValue(ladder, body);
            }
#pragma warning restore SYSLIB0050
            openGateBodyMode = mode;
            var behavior = new SiegeInteractionDebugBehavior(Mock.Of<IMessageBroker>());
            var origin = new Vec3(424f, 684f, 14f);
            StandingPoint point = fork ? pickup : lift;
            Assert.Equal(origin, behavior.GetStagingTarget(ladder, origin, true));
            if (mode == 3) point = null;
            if (mode != 0)
            {
                Assert.Throws<InvalidOperationException>(() => behavior.GetStagingTarget(ladder, origin, false, standingPoint: point));
                return;
            }
            Assert.Equal(new Vec3(fork ? 611f : 614f, 626f, 63f),
                behavior.GetStagingTarget(ladder, origin, false, standingPoint: point));
            var evidence = JObject.FromObject(AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "nativeAimTarget").GetValue(behavior));
            Assert.Equal(fork ? "fork" : "movement", evidence["ladderAction"].Value<string>());
            Assert.Equal((fork ? 881UL : 882UL).ToString("X16"), evidence["bodyPointer"].Value<string>());
            Assert.Null(AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "capturedAgent").GetValue(behavior));
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }

    private static bool OpenGatePhysicsCenter(WeakGameEntity __instance, ref Vec3 __result)
    {
        bool right = __instance.Pointer.ToUInt64() == 881UL;
        Assert.Contains(__instance.Pointer.ToUInt64(), new ulong[] { 881, 882 });
        __result = new Vec3(right ? (openGateBodyMode == 2 ? float.NaN : 611f) : 614f, 626f, 63f);
        return false;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void AmmoSupply_BindsSharedCurrentPointAndRejectsMissingOrAmbiguousPile(int ownerCount)
    {
        var harmony = new Harmony("coop.tests.ammo-supply");
        try
        {
            harmony.Patch(AccessTools.Method(typeof(ScriptComponentBehavior), "CacheEditableFieldsForAllScriptComponents"),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(SiegeInteractionDebugBehaviorTests), nameof(SkipScriptComponentCache))));
#pragma warning disable SYSLIB0050
            var weapon = (Mangonel)FormatterServices.GetUninitializedObject(typeof(Mangonel));
            var pickup = (StandingPoint)FormatterServices.GetUninitializedObject(typeof(StandingPoint));
            var load = (StandingPointWithWeaponRequirement)FormatterServices.GetUninitializedObject(typeof(StandingPointWithWeaponRequirement));
            var unrelated = (StandingPoint)FormatterServices.GetUninitializedObject(typeof(StandingPoint));
            AccessTools.Property(typeof(MissionObject), "Id").SetValue(weapon, new MissionObjectId(1554, false));
            AccessTools.Property(typeof(MissionObject), "Id").SetValue(pickup, new MissionObjectId(1556, false));
            AccessTools.Property(typeof(MissionObject), "Id").SetValue(load, new MissionObjectId(1546, false));
            AccessTools.Property(typeof(UsableMachine), "StandingPoints").SetValue(weapon, new MBList<StandingPoint> { load, pickup });
            AccessTools.Property(typeof(UsableMachine), "AmmoPickUpPoints").SetValue(weapon, new List<StandingPoint> { pickup, unrelated });
            AccessTools.Field(typeof(RangedSiegeWeapon), "LoadAmmoStandingPoint").SetValue(weapon, load);
            var piles = Enumerable.Range(0, ownerCount).Select(index =>
            {
                var pile = (SiegeMachineStonePile)FormatterServices.GetUninitializedObject(typeof(SiegeMachineStonePile));
                AccessTools.Property(typeof(MissionObject), "Id").SetValue(pile, new MissionObjectId(1564 + index, false));
                AccessTools.Property(typeof(UsableMachine), "StandingPoints").SetValue(pile, new MBList<StandingPoint> { pickup });
                return pile;
            }).ToArray();
#pragma warning restore SYSLIB0050
            var supply = JObject.FromObject(SiegeInteractionDebugBehavior.DescribeAmmoSupply(weapon, piles));
            Assert.Equal(1554, (int)supply["weaponId"]);
            Assert.Equal(1546, (int)supply["loadPointId"]);
            Assert.Equal(0, (int)supply["loadPointIndex"]);
            var selected = Assert.Single(supply["pickupPoints"]);
            Assert.Equal(1556, (int)selected["pointId"]);
            Assert.Equal(ownerCount == 1 ? (int?)1564 : null, (int?)selected["machineId"]);
            Assert.Equal(ownerCount == 1 ? 0 : -1, (int)selected["pointIndex"]);
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }

    private static bool clearForkOnDrop;
    private static int forkDropCalls;

    [Theory]
    [InlineData("clear", false)]
    [InlineData("retained", false)]
    [InlineData("foreign", false)]
    [InlineData("occupied-baseline", false)]
    [InlineData("clear", true)]
    [InlineData("retained", true)]
    [InlineData("foreign", true)]
    [InlineData("occupied-baseline", true)]
    public void RestoreFork_RequiresTheCapturedEmptySlotAndVerifiedOwnedDrop(string mode, bool ammo)
    {
        var harmony = new Harmony("coop.tests.fork-restore");
        try
        {
            harmony.Patch(AccessTools.PropertyGetter(typeof(MissionWeapon), nameof(MissionWeapon.IsEmpty)),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(SiegeInteractionDebugBehaviorTests), nameof(ForkWeaponEmpty))));
            harmony.Patch(AccessTools.Method(typeof(Agent), nameof(Agent.DropItem)),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(SiegeInteractionDebugBehaviorTests), nameof(DropOwnedFork))));
#pragma warning disable SYSLIB0050
            var agent = (Agent)FormatterServices.GetUninitializedObject(typeof(Agent));
            var fork = (ItemObject)FormatterServices.GetUninitializedObject(typeof(ItemObject));
            var other = (ItemObject)FormatterServices.GetUninitializedObject(typeof(ItemObject));
#pragma warning restore SYSLIB0050
            AccessTools.Property(typeof(Agent), nameof(Agent.Equipment)).SetValue(agent, new MissionEquipment());
            object weapon = default(MissionWeapon);
            typeof(MissionWeapon).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                .Single(field => field.FieldType == typeof(ItemObject)).SetValue(weapon, mode == "foreign" ? other : fork);
            agent.Equipment[EquipmentIndex.ExtraWeaponSlot] = (MissionWeapon)weapon;
            Assert.False(agent.Equipment[EquipmentIndex.ExtraWeaponSlot].IsEmpty);
            var behavior = new SiegeInteractionDebugBehavior(Mock.Of<IMessageBroker>());
            AccessTools.Field(typeof(SiegeInteractionDebugBehavior), ammo ? "ownedAmmoItem" : "ownedForkItem").SetValue(behavior, fork);
            AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "capturedExtraSlotEmpty").SetValue(behavior, mode != "occupied-baseline");
            clearForkOnDrop = mode == "clear";
            forkDropCalls = 0;

            Assert.Equal(mode == "clear", behavior.RestoreFork(agent));
            Assert.Equal(mode == "clear", agent.Equipment[EquipmentIndex.ExtraWeaponSlot].IsEmpty);
            Assert.Equal(mode == "clear" || mode == "retained" ? 1 : 0, forkDropCalls);
            if (mode == "clear")
            {
                Assert.True(behavior.RestoreFork(agent));
                Assert.Equal(1, forkDropCalls);
            }
        }
        finally { harmony.UnpatchAll(harmony.Id); }
    }

    private static bool DropOwnedFork(Agent __instance, EquipmentIndex itemIndex)
    {
        Assert.Equal(EquipmentIndex.ExtraWeaponSlot, itemIndex);
        forkDropCalls++;
        if (clearForkOnDrop) __instance.Equipment[itemIndex] = MissionWeapon.Invalid;
        return false;
    }

    private static bool ForkWeaponEmpty(ref MissionWeapon __instance, ref bool __result)
    {
        // The inert weapon has no native subweapon data; only slot ownership is exercised.
        __result = __instance.Item == null;
        return false;
    }

    private static bool GatePhysicsBounds(ref (Vec3, Vec3) __result)
    {
        __result = (new Vec3(612f, 623f, 60f), new Vec3(614f, 627f, 64f));
        return false;
    }

    [Fact]
    public void PassingCaptureGuard_DoesNotOverwritePriorDiagnostic()
    {
        var behavior = new SiegeInteractionDebugBehavior(Mock.Of<IMessageBroker>());
        Assert.True(behavior.RejectCapture(true, "agent_mounted"));
        Assert.False(behavior.RejectCapture(false, "agent_inactive"));

        Assert.Equal("fixture_capture_rejected",
            AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "status").GetValue(behavior));
        Assert.Equal("agent_mounted",
            AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "captureFailureReason").GetValue(behavior));
    }
}
#endif
