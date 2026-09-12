#if DEBUG
using Common.Messaging;
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
    public void FocusDiagnosticsWithoutStaging_DoesNotAccessNativeState()
    {
        var behavior = new SiegeInteractionDebugBehavior(Mock.Of<IMessageBroker>());
        Assert.Null(AccessTools.Method(typeof(SiegeInteractionDebugBehavior), "ReadFocusDiagnostic")
            .Invoke(behavior, new object[] { null, null }));
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
