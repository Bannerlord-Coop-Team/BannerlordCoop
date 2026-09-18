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
using TaleWorlds.Core;

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public class SiegeInteractionDebugBehaviorTests
{
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
    [InlineData(false)]
    [InlineData(true)]
    public void NativeTarget_UsesCurrentPhysicsCenterWithoutChangingObserverTarget(bool ballistaTarget)
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
            var machine = (UsableMachine)FormatterServices.GetUninitializedObject(
                ballistaTarget ? typeof(Ballista) : typeof(StonePile));
            if (ballistaTarget)
            {
                var body = (SynchedMissionObject)FormatterServices.GetUninitializedObject(typeof(SynchedMissionObject));
                var entity = AccessTools.Constructor(typeof(WeakGameEntity), new[] { typeof(UIntPtr) })
                    .Invoke(new object[] { new UIntPtr(880u) });
                AccessTools.Field(typeof(ScriptComponentBehavior), "_gameEntity").SetValue(body, entity);
                AccessTools.Property(typeof(Ballista), "ballistaBody").SetValue(machine, body);
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

            Assert.Equal(standingPosition, behavior.GetStagingTarget(machine, standingPosition, true, true));
            Assert.Equal(standingPosition, behavior.GetStagingTarget(machine, standingPosition, false));
            var expectedX = ballistaTarget ? 500f : 499f;
            Assert.Equal(new Vec3(expectedX, 721f, 36.5f),
                behavior.GetStagingTarget(machine, standingPosition, false, true));
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

    private static bool clearForkOnDrop;
    private static int forkDropCalls;

    [Theory]
    [InlineData("clear")]
    [InlineData("retained")]
    [InlineData("foreign")]
    [InlineData("occupied-baseline")]
    public void RestoreFork_RequiresTheCapturedEmptySlotAndVerifiedOwnedDrop(string mode)
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
            AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "ownedForkItem").SetValue(behavior, fork);
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
