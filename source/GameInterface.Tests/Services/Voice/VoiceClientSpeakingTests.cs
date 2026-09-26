using Common.Voice;
using GameInterface.Services.Voice;
using Moq;
using Xunit;

namespace GameInterface.Tests.Services.Voice;

/// <summary>Checks that the local speaking indicator follows transmitted speech and voice eligibility.</summary>
public partial class VoiceClientTests
{
    // Only transmitted non-silent audio lights the local party, with a short tail between frames.
    [Fact]
    public void LocalSpeakingRequiresSpeechAndExpiresWithoutNewFrames()
    {
        using var client = Create();
        VoiceInputSnapshot? captured = null;
        audio.Setup(x => x.Update(It.IsAny<VoiceInputSnapshot>(), It.IsAny<VoiceSettings>()))
            .Callback<VoiceInputSnapshot, VoiceSettings>((value, _) => captured = value);
        Sample();
        client.Tick();
        Assert.False(client.IsTransmitting);
        audio.Raise(x => x.Encoded += null, captured!, new byte[] { 1 });
        Assert.False(client.IsTransmitting);
        audio.SetupGet(x => x.InputLevel).Returns(0.2f);
        audio.Raise(x => x.Encoded += null, captured!, new byte[] { 1 });
        Assert.True(client.IsTransmitting);
        clock.SetupGet(x => x.Milliseconds).Returns(1201);
        sampled = new VoiceInputSnapshot(new VoicePosition("campaign", 0, 0, 0, 0, true, true), true, false, true, 1201);
        client.Tick();
        Assert.False(client.IsTransmitting);
    }

    // Changing voice eligibility or hiding talking players removes the local icon immediately.
    [Theory]
    [InlineData("muted")]
    [InlineData("deafened")]
    [InlineData("hidden")]
    [InlineData("disabled")]
    [InlineData("context")]
    [InlineData("capture")]
    [InlineData("microphone-test")]
    [InlineData("server-disabled")]
    public void LocalSpeakingClearsWhenVoiceBecomesIneligible(string reason)
    {
        using var client = Create();
        VoiceInputSnapshot? captured = null;
        audio.Setup(x => x.Update(It.IsAny<VoiceInputSnapshot>(), It.IsAny<VoiceSettings>()))
            .Callback<VoiceInputSnapshot, VoiceSettings>((value, _) => captured = value);
        audio.SetupGet(x => x.InputLevel).Returns(0.2f);
        Sample();
        client.Tick();
        audio.Raise(x => x.Encoded += null, captured!, new byte[] { 1 });
        Assert.True(client.IsTransmitting);
        var settings = client.Settings;
        switch (reason)
        {
            case "muted": settings.Muted = true; client.Apply(settings); break;
            case "deafened": settings.Deafened = true; client.Apply(settings); break;
            case "hidden": settings.ShowTalkingPlayers = false; client.Apply(settings); break;
            case "disabled": settings.Enabled = false; client.Apply(settings); break;
            case "context": Sample("settlement"); client.Tick(); break;
            case "capture": client.SetKeybindCapture(true); break;
            case "microphone-test": client.TestMicrophone(true); break;
            case "server-disabled": client.SetServerEnabled(false); break;
        }
        Assert.False(client.IsTransmitting);
    }
}
