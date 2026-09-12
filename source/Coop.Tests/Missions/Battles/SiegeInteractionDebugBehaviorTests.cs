#if DEBUG
using Common.Messaging;
using Missions.Battles;
using Moq;
using HarmonyLib;
using Xunit;

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
