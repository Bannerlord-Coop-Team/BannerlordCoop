using Common.Voice;
using System;
using System.Linq;
using System.Threading;
using Xunit;

namespace GameInterface.Tests.Services.Voice;

public partial class VoiceAudioTests
{
    [Fact]
    public void TalkingStartsOnlyAfterAudiblePlayoutAndExpiresWithoutFreshSpeech()
    {
        using var h = new WorkerHarness();
        h.Start();
        h.Frame(1);
        Wait(() => h.ReceivedCount == 1);
        Assert.Empty(h.Audio.AudibleSpeakers);
        h.Advance(1060);
        Wait(() => h.Audio.AudibleSpeakers.Count == 1);
        Assert.Equal("speaker", Assert.Single(h.Audio.AudibleSpeakers));
        h.Advance(1280);
        Assert.Empty(h.Audio.AudibleSpeakers);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.001f)]
    public void ZeroOrInaudibleGainDoesNotIdentifyASpeaker(float volume)
    {
        using var h = new WorkerHarness();
        h.Options.Volume = volume;
        h.Start(); h.Frame(1);
        Wait(() => h.ReceivedCount == 1);
        h.Advance(1060);
        Wait(() => h.Played.Count > 0);
        Assert.Empty(h.Audio.AudibleSpeakers);
    }

    [Fact]
    public void DecodedSilenceIsNotTalking()
    {
        using var h = new WorkerHarness(realCodec: true);
        h.Start();
        var encoded = new global::GameInterface.Services.Voice.OpusVoiceCodecFactory().CreateEncoder().Encode(new short[960]);
        h.Audio.Receive(new VoicePacket
        {
            Position = new VoicePosition("campaign", 1, 0, 0, 0, true, true), ListenerEpoch = 1,
            Sequence = 1, Audio = encoded, Speaker = "platform:1", Gain = 1, StreamGeneration = 1
        });
        Wait(() => h.ReceivedCount == 1);
        h.Advance(1060); Wait(() => h.Played.Count > 0);
        Assert.Empty(h.Audio.AudibleSpeakers);
    }

    [Fact]
    public void DisabledEffectiveSettingsDiscardAlreadySubmittedOutputAndActivity()
    {
        using var h = new WorkerHarness();
        var outputs = UseBufferedOutputs(h);
        h.Start();
        var output = outputs.Single();
        QueueRemoteAudioInProviderAndHardware(h, output);
        Wait(() => h.Audio.AudibleSpeakers.Count == 1);
        h.Options.Enabled = false;
        h.Sample(epoch: 2);
        Assert.Empty(h.Audio.AudibleSpeakers);
        Wait(() => output.IsDisposed);
        Assert.Equal(0, output.HardwareCount);
        Assert.Equal(0, output.ProviderCount);
        h.Options.Enabled = true;
        h.Sample(epoch: 3);
        Wait(() => outputs.Count == 2);
        Assert.Empty(h.Audio.AudibleSpeakers);
        Assert.Equal(0, outputs.Last().ProviderCount);
    }

    [Fact]
    public void ListenerContextResetImmediatelyClearsSpeakingActivity()
    {
        using var h = new WorkerHarness();
        h.Start(); h.Frame(1); Wait(() => h.ReceivedCount == 1);
        h.Advance(1060); Wait(() => h.Audio.AudibleSpeakers.Count == 1);
        h.Sample(epoch: 2);
        Assert.Empty(h.Audio.AudibleSpeakers);
    }

    [Fact]
    public void NewPlatformStreamAcceptsLowerEpochAndRetiredGenerationStaysRejectedAfterReset()
    {
        using var h = new WorkerHarness();
        h.Start();
        void Frame(long generation, long epoch, long listener = 1) => h.Audio.Receive(new VoicePacket
        {
            Position = new VoicePosition("campaign", epoch, 0, 0, 0, true, true), ListenerEpoch = listener,
            Sequence = 1, Audio = new byte[] { 1 }, Speaker = "platform:1", Gain = 1, StreamGeneration = generation
        });
        Frame(1, 20); Wait(() => h.ReceivedCount == 1);
        Frame(2, 1); Wait(() => h.ReceivedCount == 2);
        Assert.Equal(2, h.DecoderCount);
        int clears = h.Clears;
        h.Sample(epoch: 2);
        Wait(() => h.Clears > clears);
        Frame(1, 30, 2);
        Thread.Sleep(40);
        Assert.Equal(2, h.ReceivedCount);
        Frame(2, 2, 2); Wait(() => h.ReceivedCount == 3);
    }
}
