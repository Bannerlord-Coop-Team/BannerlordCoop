using Common.Voice;
using Xunit;

namespace Common.Tests.Voice;

public class VoicePolicyTests
{
    private readonly VoicePolicy policy = new();
    private VoicePosition At(float x, string context = "campaign", bool speak = true, bool hear = true)
        => new(context, 1, x, 0, 0, speak, hear);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(5, 1)]
    [InlineData(12.5f, 0.5f)]
    [InlineData(20, 0)]
    [InlineData(100, 0)]
    public void CampaignFalloff(float distance, float expected)
        => Assert.Equal(expected, policy.Gain(At(0), At(distance), new VoiceRanges()), 5);

    [Theory]
    [InlineData("scene:town|tavern", 10, 1)]
    [InlineData("scene:town|tavern", 30, 0.5f)]
    [InlineData("scene:town|tavern", 50, 0)]
    [InlineData("battle:event", 10, 1)]
    [InlineData("battle:event", 30, 0.5f)]
    [InlineData("battle:event", 50, 0)]
    [InlineData("scene:tournament:session", 10, 1)]
    [InlineData("scene:tournament:session", 30, 0.5f)]
    [InlineData("scene:tournament:session", 50, 0)]
    public void SceneFalloffUsesSceneRangesAndThreeDimensionalDistance(string context, float distance, float expected)
    {
        var speaker = new VoicePosition(context, 1, 0, 0, 0, true, true);
        var listener = new VoicePosition(context, 1, distance * 0.6f, 0, distance * 0.8f, false, true);
        Assert.Equal(expected, policy.Gain(speaker, listener, new VoiceRanges()), 5);
    }

    [Fact]
    public void SeparatesScenesAndAllowsReceiveOnlySpectators()
    {
        Assert.Equal(0, policy.Gain(At(0, "battle:a"), At(0, "battle:b"), new VoiceRanges()));
        Assert.Equal(0, policy.Gain(At(0, speak: false), At(0), new VoiceRanges()));
        Assert.Equal(1, policy.Gain(At(0), At(0, speak: false), new VoiceRanges()));
        Assert.Equal(0, policy.Gain(At(0), At(0, hear: false), new VoiceRanges()));
        Assert.Equal(0, policy.Gain(At(float.NaN), At(0), new VoiceRanges()));
    }

    [Theory]
    [InlineData(false, false, false, false, true)]
    [InlineData(true, true, false, false, true)]
    [InlineData(true, false, true, false, true)]
    [InlineData(true, false, false, true, true)]
    [InlineData(true, false, false, false, false)]
    public void TransmitGates(bool focused, bool typing, bool muted, bool deafened, bool speaking)
        => Assert.False(policy.CanTransmit(VoiceActivation.PushToTalk, speaking, focused, typing, muted, deafened, true, 1, 0.1f));

    [Fact]
    public void ActivationAndDisabledModes()
    {
        Assert.True(policy.CanTransmit(VoiceActivation.PushToTalk, true, true, false, false, false, true, 0, 0.1f));
        Assert.False(policy.CanTransmit(VoiceActivation.Disabled, true, true, false, false, false, true, 1, 0.1f));
        Assert.True(policy.CanTransmit(VoiceActivation.VoiceActivity, true, true, false, false, false, false, 0.2f, 0.1f));
        Assert.False(policy.CanTransmit(VoiceActivation.VoiceActivity, true, true, false, false, false, false, 0.01f, 0.1f));
    }

    [Theory]
    [InlineData(-1, 20)]
    [InlineData(20, 20)]
    [InlineData(float.NaN, 20)]
    [InlineData(5, float.PositiveInfinity)]
    public void RejectsInvalidRanges(float full, float maximum)
        => Assert.False(new VoiceRanges(full, maximum).IsValid);
}
