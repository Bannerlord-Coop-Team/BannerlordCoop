#if DEBUG
using Common.Voice;
using GameInterface.Services.Voice;
using Moq;
using System;
using System.Linq;
using Xunit;

namespace GameInterface.Tests.Services.Voice;

public partial class VoiceClientTests
{
    private void SyntheticSample(long now, bool focused = false, bool pressed = false, string context = "campaign", bool speak = true, bool typing = false)
    {
        clock.SetupGet(x => x.Milliseconds).Returns(now);
        sampled = new VoiceInputSnapshot(new VoicePosition(context, 0, 4, 5, 0, speak, true), focused, typing, pressed, now);
    }

    private VoicePacket[] SubmittedSyntheticPackets() => network.Invocations
        .SelectMany(x => x.Arguments).OfType<VoicePacket>().Where(x => x.Audio.Length > 0).ToArray();

    private sealed class SyntheticEncoder : IVoiceEncoder, IDisposable
    {
        public bool Disposed;
        public Action? Encoding;
        public byte[] Encode(short[] samples)
        {
            Assert.Equal(960, samples.Length);
            Assert.All(samples, value => Assert.InRange(value, -2000, 2000));
            Encoding?.Invoke();
            return new OpusVoiceCodecFactory().CreateEncoder().Encode(samples);
        }
        public void Dispose() => Disposed = true;
    }

    private static IVoiceCodecFactory SyntheticFactory(SyntheticEncoder encoder)
    {
        var factory = new Mock<IVoiceCodecFactory>();
        factory.Setup(x => x.CreateEncoder()).Returns(encoder);
        return factory.Object;
    }

    [Fact]
    public void SyntheticToneUsesRealOpusAndPositionWithoutChangingNormalCallbackFocusOrPttGates()
    {
        using var client = Create();
        VoiceInputSnapshot? captured = null;
        audio.Setup(x => x.Update(It.IsAny<VoiceInputSnapshot>(), It.IsAny<VoiceSettings>()))
            .Callback<VoiceInputSnapshot, VoiceSettings>((value, _) => captured = value);
        SyntheticSample(1000); client.Tick();
        audio.Raise(x => x.Encoded += null, captured!, new byte[] { 1 });
        Assert.Empty(SubmittedSyntheticPackets());
        Assert.Contains("running", client.StartSyntheticVoiceTest(new OpusVoiceCodecFactory()));
        client.Tick();
        var packet = Assert.Single(SubmittedSyntheticPackets());
        Assert.Null(packet.Speaker);
        Assert.Equal(0, packet.StreamGeneration);
        Assert.Equal("campaign", packet.Position.Context);
        Assert.Equal(4, packet.Position.X);
        Assert.Equal(captured!.Position.Epoch, packet.Position.Epoch);
        Assert.False(captured.Focused);
        Assert.False(captured.PushToTalk);
        Assert.Contains(new OpusVoiceCodecFactory().CreateDecoder().Decode(packet.Audio), value => value != 0);
        audio.Raise(x => x.Encoded += null, captured, new byte[] { 1 });
        Assert.Single(SubmittedSyntheticPackets());
        client.StopSyntheticVoiceTest();
        SyntheticSample(1020, focused: true); client.Tick();
        audio.Raise(x => x.Encoded += null, captured!, new byte[] { 1 });
        Assert.Single(SubmittedSyntheticPackets());
        SyntheticSample(1040, focused: true, pressed: true); client.Tick();
        audio.Raise(x => x.Encoded += null, captured!, new byte[] { 1 });
        Assert.Equal(2, SubmittedSyntheticPackets().Length);
        Assert.True(SubmittedSyntheticPackets()[1].Sequence > packet.Sequence);
    }

    [Fact]
    public void SyntheticToneHasHardDeadlineRejectsOverlapAndNeverCatchesUpStalledFrames()
    {
        using var client = Create();
        SyntheticSample(1000); client.Tick();
        var encoder = new SyntheticEncoder();
        client.StartSyntheticVoiceTest(SyntheticFactory(encoder));
        client.Tick(); client.Tick();
        Assert.Single(SubmittedSyntheticPackets());
        SyntheticSample(5999); client.Tick();
        Assert.Contains("already running", client.StartSyntheticVoiceTest(SyntheticFactory(encoder)));
        Assert.Equal(2, SubmittedSyntheticPackets().Length);
        SyntheticSample(6000); client.Tick();
        Assert.Equal(2, SubmittedSyntheticPackets().Length);
        Assert.True(encoder.Disposed);
        Assert.Contains("stopped", client.SyntheticVoiceTestStatus);
        SyntheticSample(6100); client.Tick();
        Assert.Equal(2, SubmittedSyntheticPackets().Length);
    }

    [Theory]
    [InlineData("server")]
    [InlineData("disabled")]
    [InlineData("mute")]
    [InlineData("deafen")]
    [InlineData("mode")]
    [InlineData("context")]
    [InlineData("spectator")]
    [InlineData("typing")]
    [InlineData("binding")]
    [InlineData("test")]
    [InlineData("dispose")]
    [InlineData("stop")]
    public void SyntheticToneCancelsWithoutLaterFramesWhenEligibilityChanges(string change)
    {
        using var client = Create();
        SyntheticSample(1000); client.Tick();
        var encoder = new SyntheticEncoder();
        client.StartSyntheticVoiceTest(SyntheticFactory(encoder));
        client.Tick();
        var settings = client.Settings;
        switch (change)
        {
            case "server": client.SetServerEnabled(false); break;
            case "disabled": settings.Enabled = false; client.Apply(settings); break;
            case "mute": settings.Muted = true; client.Apply(settings); break;
            case "deafen": settings.Deafened = true; client.Apply(settings); break;
            case "mode": settings.Activation = VoiceActivation.VoiceActivity; client.Apply(settings); break;
            case "binding": client.SetKeybindCapture(true); break;
            case "test": client.TestMicrophone(true); break;
            case "dispose": client.Dispose(); break;
            case "stop": client.StopSyntheticVoiceTest(); break;
        }
        SyntheticSample(1020, context: change == "context" ? "another" : "campaign", speak: change != "spectator", typing: change == "typing");
        client.Tick();
        Assert.True(encoder.Disposed);
        Assert.Single(SubmittedSyntheticPackets());
        Assert.Contains("stopped", client.SyntheticVoiceTestStatus);
    }

    [Theory]
    [InlineData("unconfigured")]
    [InlineData("stale")]
    [InlineData("spectator")]
    [InlineData("context")]
    [InlineData("server")]
    [InlineData("mute")]
    [InlineData("deafen")]
    [InlineData("disabled")]
    [InlineData("test")]
    [InlineData("binding")]
    public void SyntheticStartRequiresCurrentSpeakingEligibility(string reason)
    {
        using var client = Create(reason != "unconfigured");
        SyntheticSample(1000, context: reason == "context" ? "" : "campaign", speak: reason != "spectator"); client.Tick();
        var settings = client.Settings;
        if (reason == "stale") clock.SetupGet(x => x.Milliseconds).Returns(1101);
        if (reason == "server") client.SetServerEnabled(false);
        if (reason == "mute") { settings.Muted = true; client.Apply(settings); }
        if (reason == "deafen") { settings.Deafened = true; client.Apply(settings); }
        if (reason == "disabled") { settings.Enabled = false; client.Apply(settings); }
        if (reason == "test") client.TestMicrophone(true);
        if (reason == "binding") client.SetKeybindCapture(true);
        var factory = new Mock<IVoiceCodecFactory>(MockBehavior.Strict);
        Assert.Contains("rejected", client.StartSyntheticVoiceTest(factory.Object));
        factory.Verify(x => x.CreateEncoder(), Times.Never);
        Assert.Empty(SubmittedSyntheticPackets());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SyntheticEncoderFailureOrDeadlineDuringEncodeDisposesAndDoesNotSend(bool expire)
    {
        using var client = Create();
        SyntheticSample(1000); client.Tick();
        var encoder = new SyntheticEncoder { Encoding = () =>
        {
            if (expire) clock.SetupGet(x => x.Milliseconds).Returns(6000);
            else throw new InvalidOperationException("test encode failure");
        }};
        client.StartSyntheticVoiceTest(SyntheticFactory(encoder)); client.Tick();
        Assert.True(encoder.Disposed);
        Assert.Empty(SubmittedSyntheticPackets());
        Assert.Contains("stopped", client.SyntheticVoiceTestStatus);
    }

    [Fact]
    public void SyntheticStaleEncodeDiscardsPayloadAndNextFreshTickSendsWithinOriginalDeadline()
    {
        using var client = Create();
        SyntheticSample(1000); client.Tick();
        var encoder = new SyntheticEncoder
        {
            Encoding = () => clock.SetupGet(x => x.Milliseconds).Returns(1101)
        };
        client.StartSyntheticVoiceTest(SyntheticFactory(encoder)); client.Tick();
        Assert.Empty(SubmittedSyntheticPackets());
        Assert.False(encoder.Disposed);
        Assert.Contains("stale frames discarded=1", client.SyntheticVoiceTestStatus);
        encoder.Encoding = null;
        SyntheticSample(1120); client.Tick();
        Assert.Single(SubmittedSyntheticPackets());
        SyntheticSample(6000); client.Tick();
        Assert.True(encoder.Disposed);
        Assert.Contains("Five-second deadline reached", client.SyntheticVoiceTestStatus);
        Assert.Single(SubmittedSyntheticPackets());
    }

    [Fact]
    public void SyntheticRepeatedStaleEncodesExpireWithoutSendingOrExtendingDeadline()
    {
        using var client = Create();
        SyntheticSample(1000); client.Tick();
        var encoder = new SyntheticEncoder();
        client.StartSyntheticVoiceTest(SyntheticFactory(encoder));
        for (long now = 1000; now < 6000; now += 120)
        {
            long completedAt = now + 101;
            encoder.Encoding = () => clock.SetupGet(x => x.Milliseconds).Returns(completedAt);
            SyntheticSample(now); client.Tick();
        }
        Assert.True(encoder.Disposed);
        Assert.Empty(SubmittedSyntheticPackets());
        Assert.Contains("Five-second deadline reached during encoding", client.SyntheticVoiceTestStatus);
        Assert.Contains("stale frames discarded=41", client.SyntheticVoiceTestStatus);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SyntheticPositionTransitionDuringEncodeStopsWithoutSending(bool changeEpoch)
    {
        using var client = Create();
        VoiceInputSnapshot? captured = null;
        audio.Setup(x => x.Update(It.IsAny<VoiceInputSnapshot>(), It.IsAny<VoiceSettings>()))
            .Callback<VoiceInputSnapshot, VoiceSettings>((value, _) => captured = value);
        SyntheticSample(1000); client.Tick();
        var encoder = new SyntheticEncoder { Encoding = () =>
        {
            var point = captured!.Position;
            var changed = new VoicePosition(changeEpoch ? point.Context : "another", changeEpoch ? point.Epoch + 1 : point.Epoch,
                point.X, point.Y, point.Z, true, true);
            typeof(VoiceClient).GetField("snapshot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .SetValue(client, new VoiceInputSnapshot(changed, false, false, false, 1000));
        }};
        client.StartSyntheticVoiceTest(SyntheticFactory(encoder)); client.Tick();
        Assert.True(encoder.Disposed);
        Assert.Empty(SubmittedSyntheticPackets());
        Assert.Contains(changeEpoch ? "Position epoch changed during encoding" : "Position context changed during encoding",
            client.SyntheticVoiceTestStatus);
    }

    [Fact]
    public void SyntheticEncoderStartFailureAndStatusExpiryAreNonfatal()
    {
        using var client = Create();
        SyntheticSample(1000); client.Tick();
        var factory = new Mock<IVoiceCodecFactory>();
        factory.Setup(x => x.CreateEncoder()).Throws(new InvalidOperationException("test create failure"));
        Assert.Contains("test create failure", client.StartSyntheticVoiceTest(factory.Object));
        var encoder = new SyntheticEncoder();
        client.StartSyntheticVoiceTest(SyntheticFactory(encoder));
        clock.SetupGet(x => x.Milliseconds).Returns(6000);
        Assert.Contains("stopped", client.SyntheticVoiceTestStatus);
        Assert.True(encoder.Disposed);
        Assert.Empty(SubmittedSyntheticPackets());
    }
}
#endif
