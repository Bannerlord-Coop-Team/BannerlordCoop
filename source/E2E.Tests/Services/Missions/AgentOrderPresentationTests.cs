using Missions.Agents.Handlers;
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

    [Theory]
    [InlineData("Everyone")]
    [InlineData("Follow")]
    [InlineData("Infantry")]
    [InlineData("FormShieldWall")]
    public void IsOrderVoice_AcceptsFormationAndOrderVoices(string voiceTypeId)
    {
        Assert.True(AgentVoiceHandler.IsOrderVoice(voiceTypeId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Pain")]
    [InlineData("Death")]
    [InlineData("Yell")]
    public void IsOrderVoice_RejectsUnrelatedCombatVoices(string? voiceTypeId)
    {
        Assert.False(AgentVoiceHandler.IsOrderVoice(voiceTypeId));
    }
}
