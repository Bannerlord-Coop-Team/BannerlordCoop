using Common.Voice;
using GameInterface.Services.Voice;
using Moq;
using System;
using System.Threading;
using Xunit;

namespace GameInterface.Tests.Services.Voice;

public partial class VoiceAudioTests
{
    private VoiceInputSnapshot Snapshot(long epoch = 1, bool hear = true)
        => new(new VoicePosition("campaign", epoch, 0, 0, 0, true, hear), true, false, true, 1000);

    [Fact]
    public void MasterDisableClosesOutputAndReenableReopensWithoutChangingSettings()
    {
        var factory = new Mock<IVoiceDeviceFactory>();
        var device = new Mock<IVoiceDevice>();
        factory.Setup(x => x.Open(It.IsAny<Action<Exception>>())).Returns(device.Object);
        var clock = new Mock<IVoiceClock>();
        clock.SetupGet(x => x.Milliseconds).Returns(1000);
        using var audio = new VoiceAudio(new OpusVoiceCodecFactory(), factory.Object, Mock.Of<IVoiceCaptureFactory>(), new VoicePolicy(), clock.Object, () => new VoiceJitterBuffer());
        audio.Update(Snapshot(), new VoiceSettings());
        Assert.True(SpinWait.SpinUntil(() => audio.Status == "Ready", 3000));
        audio.Update(Snapshot(2, false), new VoiceSettings { Enabled = false });
        Assert.True(SpinWait.SpinUntil(() => audio.Status == "Disabled", 3000));
        device.Verify(x => x.Dispose(), Times.Once);
        factory.Verify(x => x.Open(It.IsAny<Action<Exception>>()), Times.Once);
        audio.Update(Snapshot(3), new VoiceSettings());
        Assert.True(SpinWait.SpinUntil(() => audio.Status == "Ready", 3000));
        factory.Verify(x => x.Open(It.IsAny<Action<Exception>>()), Times.Exactly(2));
    }

    [Fact]
    public void DeviceFailureCanRetryAndLateOldCallbackCannotFaultReplacement()
    {
        var factory = new Mock<IVoiceDeviceFactory>();
        var first = new Mock<IVoiceDevice>();
        var second = new Mock<IVoiceDevice>();
        Action<Exception>? oldFailure = null;
        int opens = 0;
        factory.Setup(x => x.Open(It.IsAny<Action<Exception>>()))
            .Returns<Action<Exception>>(failed =>
            {
                int number = Interlocked.Increment(ref opens);
                if (number == 1) { oldFailure = failed; return first.Object; }
                return second.Object;
            });
        var clock = new Mock<IVoiceClock>();
        clock.SetupGet(x => x.Milliseconds).Returns(1000);
        using var audio = new VoiceAudio(new OpusVoiceCodecFactory(), factory.Object, Mock.Of<IVoiceCaptureFactory>(), new VoicePolicy(), clock.Object, () => new VoiceJitterBuffer());
        audio.Update(Snapshot(), new VoiceSettings());
        Assert.True(SpinWait.SpinUntil(() => audio.Status == "Ready", 3000));
        oldFailure!(new InvalidOperationException("unplugged"));
        Assert.Contains("unplugged", audio.Status);
        audio.Retry();
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref opens) == 2 && audio.Status == "Ready", 3000));
        oldFailure(new InvalidOperationException("old device"));
        Assert.Equal("Ready", audio.Status);
        audio.Dispose(); audio.Dispose();
        first.Verify(x => x.Dispose(), Times.Once);
        second.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public void DisabledModeNeverOpensDevice()
    {
        var factory = new Mock<IVoiceDeviceFactory>();
        var clock = new Mock<IVoiceClock>();
        clock.SetupGet(x => x.Milliseconds).Returns(1000);
        using var audio = new VoiceAudio(new OpusVoiceCodecFactory(), factory.Object, Mock.Of<IVoiceCaptureFactory>(), new VoicePolicy(), clock.Object, () => new VoiceJitterBuffer());
        audio.Update(Snapshot(), new VoiceSettings { Activation = VoiceActivation.Disabled });
        Assert.True(SpinWait.SpinUntil(() => audio.Status == "Disabled", 3000));
        factory.Verify(x => x.Open(It.IsAny<Action<Exception>>()), Times.Never);
    }

    [Fact]
    public void RejectsOldListenerEpochBeforeDecoderOrPlayback()
    {
        var factory = new Mock<IVoiceDeviceFactory>();
        var device = new Mock<IVoiceDevice>();
        factory.Setup(x => x.Open(It.IsAny<Action<Exception>>())).Returns(device.Object);
        var codecs = new Mock<IVoiceCodecFactory>();
        codecs.Setup(x => x.CreateEncoder()).Returns(Mock.Of<IVoiceEncoder>());
        var clock = new Mock<IVoiceClock>();
        clock.SetupGet(x => x.Milliseconds).Returns(1000);
        using var audio = new VoiceAudio(codecs.Object, factory.Object, Mock.Of<IVoiceCaptureFactory>(), new VoicePolicy(), clock.Object, () => new VoiceJitterBuffer());
        audio.Update(Snapshot(2), new VoiceSettings());
        Assert.True(SpinWait.SpinUntil(() => audio.Status == "Ready", 3000));
        audio.Receive(new VoicePacket
        {
            Position = new VoicePosition("campaign", 1, 0, 0, 0, true, true), ListenerEpoch = 1,
            Audio = new byte[] { 1 }, Speaker = "old", Gain = 1
        });
        audio.Dispose();
        codecs.Verify(x => x.CreateDecoder(), Times.Never);
        device.Verify(x => x.Play(It.IsAny<byte[]>()), Times.Never);
    }

    [Fact]
    public void TenSpeakersMixWithoutOverflowAndEleventhDoesNotAllocateDecoder()
    {
        long now = 1000;
        int decoders = 0;
        var factory = new Mock<IVoiceDeviceFactory>();
        var device = new Mock<IVoiceDevice>();
        byte[]? played = null;
        device.Setup(x => x.Play(It.IsAny<byte[]>())).Callback<byte[]>(bytes => played = bytes);
        factory.Setup(x => x.Open(It.IsAny<Action<Exception>>())).Returns(device.Object);
        var codecs = new Mock<IVoiceCodecFactory>();
        codecs.Setup(x => x.CreateEncoder()).Returns(Mock.Of<IVoiceEncoder>());
        codecs.Setup(x => x.CreateDecoder()).Returns(() =>
        {
            Interlocked.Increment(ref decoders);
            var decoder = new Mock<IVoiceDecoder>();
            var samples = new short[960];
            Array.Fill(samples, short.MaxValue);
            decoder.Setup(x => x.Decode(It.IsAny<byte[]>())).Returns(samples);
            return decoder.Object;
        });
        var clock = new Mock<IVoiceClock>();
        clock.SetupGet(x => x.Milliseconds).Returns(() => Interlocked.Read(ref now));
        using var audio = new VoiceAudio(codecs.Object, factory.Object, Mock.Of<IVoiceCaptureFactory>(), new VoicePolicy(), clock.Object, () => new VoiceJitterBuffer());
        audio.Update(Snapshot(), new VoiceSettings());
        Assert.True(SpinWait.SpinUntil(() => audio.Status == "Ready", 3000));
        for (int i = 0; i < 11; i++)
            audio.Receive(new VoicePacket
            {
                Position = new VoicePosition("campaign", 1, 0, 0, 0, true, true), ListenerEpoch = 1,
                Audio = new byte[] { 1 }, Speaker = "speaker" + i, Gain = 1, Sequence = 1
            });
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref decoders) == 10, 3000));
        Interlocked.Exchange(ref now, 1060);
        Assert.True(SpinWait.SpinUntil(() => played != null, 3000));
        Assert.Equal(10, decoders);
        Assert.Equal(1920, played!.Length);
        Assert.InRange(BitConverter.ToInt16(played, 0), (short)32000, short.MaxValue);
    }

    [Fact]
    public void ManagedCodecRoundTripsTwentyMillisecondsAndConcealsLoss()
    {
        var factory = new OpusVoiceCodecFactory();
        var samples = new short[960];
        for (int i = 0; i < samples.Length; i++) samples[i] = (short)(Math.Sin(i * 0.06) * 8000);
        byte[] encoded = factory.CreateEncoder().Encode(samples);
        Assert.InRange(encoded.Length, 1, VoicePacket.MaximumPayload);
        var decoder = factory.CreateDecoder();
        Assert.Equal(960, decoder.Decode(encoded).Length);
        Assert.Equal(960, decoder.Decode(null!).Length);
    }
}
