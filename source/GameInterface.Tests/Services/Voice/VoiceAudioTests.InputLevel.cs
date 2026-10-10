using GameInterface.Services.Voice;
using System;
using System.Linq;
using Xunit;

namespace GameInterface.Tests.Services.Voice;

public partial class VoiceAudioTests
{
    [Theory]
    [InlineData(short.MinValue, 1f)]
    [InlineData((short)16384, 0.5f)]
    [InlineData((short)0, 0f)]
    public void InputLevelUsesNormalizedCaptureAmplitudeAndDecaysWhenCallbacksStop(short sample, float expected)
    {
        using var h = new WorkerHarness();
        Assert.Equal(0, h.Audio.InputLevel);
        h.Start();
        h.Callbacks.Last().data(Pcm(sample), 1920);
        Assert.Equal(expected, h.Audio.InputLevel);
        h.Advance(1050);
        Assert.Equal(expected * 0.5f, h.Audio.InputLevel);
        h.Advance(1100);
        Assert.Equal(0, h.Audio.InputLevel);
        h.Callbacks.Last().data(Pcm(sample), 1920);
        Assert.Equal(expected, h.Audio.InputLevel);
        h.Callbacks.Last().data(Pcm(0), 1920);
        Assert.Equal(0, h.Audio.InputLevel);
    }

    [Theory]
    [InlineData("mute")]
    [InlineData("deafen")]
    [InlineData("disabled")]
    [InlineData("focus")]
    [InlineData("typing")]
    [InlineData("ptt")]
    [InlineData("context")]
    [InlineData("failure")]
    [InlineData("dispose")]
    public void InputLevelResetsWhenCaptureIsNoLongerPermitted(string reason)
    {
        using var h = new WorkerHarness();
        h.Start();
        var capture = h.Callbacks.Last();
        capture.data(Pcm(16384), 1920);
        Assert.Equal(0.5f, h.Audio.InputLevel);
        h.Options = new VoiceSettings
        {
            Muted = reason == "mute", Deafened = reason == "deafen", Enabled = reason != "disabled"
        };
        h.Sample(epoch: reason == "context" ? 2 : 1, focused: reason != "focus", typing: reason == "typing", ptt: reason != "ptt");
        if (reason == "failure") capture.error(new InvalidOperationException("unplugged"));
        if (reason == "dispose") h.Audio.Dispose();
        Assert.Equal(0, h.Audio.InputLevel);
        capture.data(Pcm(16384), 1920);
        Assert.Equal(0, h.Audio.InputLevel);
    }

    [Fact]
    public void LocalTestFeedsMeterWithoutTransmissionAndStopOrFailureResetsBoth()
    {
        using var h = new WorkerHarness();
        h.Options.Muted = true;
        h.Start();
        h.Audio.TestMicrophone(true);
        Wait(() => h.Callbacks.Count == 2);
        h.Callbacks.Last().data(Pcm(16384), 1920);
        Assert.True(h.Audio.IsTestingMicrophone);
        Assert.Equal(0.5f, h.Audio.InputLevel);
        Wait(() => !h.Played.IsEmpty);
        Assert.Empty(h.Encoded);
        h.Audio.TestMicrophone(false);
        Assert.False(h.Audio.IsTestingMicrophone);
        Assert.Equal(0, h.Audio.InputLevel);
        Wait(() => h.Callbacks.Count == 3);
        h.Audio.TestMicrophone(true);
        Wait(() => h.Callbacks.Count == 4);
        h.Callbacks.Last().data(Pcm(16384), 1920);
        h.Callbacks.Last().error(new InvalidOperationException("unplugged"));
        Assert.False(h.Audio.IsTestingMicrophone);
        Assert.Equal(0, h.Audio.InputLevel);
        Assert.Contains("unplugged", h.Audio.Status);
        Assert.Empty(h.Encoded);
    }
}
