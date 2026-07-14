using Missions.Agents.Handlers;
using Missions.Agents.Patches;
using Xunit;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Regression coverage for classifying player order gestures and voices.
/// </summary>
public class AgentOrderPresentationTests
{
    [Theory]
    [InlineData("act_command")]
    [InlineData("act_command_follow_unarmed")]
    [InlineData("act_horse_command")]
    [InlineData("act_horse_command_follow_2h")]
    public void IsOrderGesture_AcceptsPlayerCommandActions(string actionName)
    {
        Assert.True(AgentActionHandler.IsOrderGesture(actionName));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("act_walk_forward")]
    [InlineData("act_mount")]
    public void IsOrderGesture_RejectsUnrelatedActions(string? actionName)
    {
        Assert.False(AgentActionHandler.IsOrderGesture(actionName));
    }

    [Fact]
    public void OrderVoiceContext_TracksNestedCalls()
    {
        Assert.False(OrderVoiceContextPatch.IsActive);

        OrderVoiceContextPatch.Enter();
        try
        {
            Assert.True(OrderVoiceContextPatch.IsActive);

            OrderVoiceContextPatch.Enter();
            try
            {
                Assert.True(OrderVoiceContextPatch.IsActive);
            }
            finally
            {
                OrderVoiceContextPatch.Exit();
            }

            Assert.True(OrderVoiceContextPatch.IsActive);
        }
        finally
        {
            OrderVoiceContextPatch.Exit();
        }

        Assert.False(OrderVoiceContextPatch.IsActive);
    }
}
