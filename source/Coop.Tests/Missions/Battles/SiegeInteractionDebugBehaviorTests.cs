#if DEBUG
using Common.Messaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Missions.Battles;
using Moq;
using HarmonyLib;
using Xunit;
using System.Runtime.Serialization;
using TaleWorlds.Library;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace Coop.Tests.Missions.Battles;

[Collection("Mission.Current")]
public class SiegeInteractionDebugBehaviorTests
{
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
        var direction = SiegeInteractionDebugBehavior.GetNativeStoneStagingDirection(userPosition, eyeHeight, targetCenter);

        Assert.True(direction.z < -0.5f);
        Assert.True(Math.Abs(direction.Length - 1f) < 0.0001f);
        var targetDistance = (targetCenter - eye).Length;
        Assert.True((eye + (direction * targetDistance) - targetCenter).Length < 0.0001f);
        var horizontal = new Vec3(direction.x, direction.y, 0f).NormalizedCopy();
        Assert.True((eye + (horizontal * targetDistance) - targetCenter).Length > 1f);
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
        Assert.InRange(samples[0]["recordedUtc"].Value<DateTime>(), before, DateTime.UtcNow);
        Assert.False((bool)AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "edgeObserved").GetValue(behavior));
        Assert.False((bool)AccessTools.Field(typeof(SiegeInteractionDebugBehavior), "edgeCleared").GetValue(behavior));
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
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    private static bool SkipScriptComponentCache() => false;

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
