using Common.Voice;
using Concentus.Enums;
using Concentus.Structs;
using GameInterface.Services.Voice;
using Moq;
using System;
using System.Linq;
using System.Threading;
using Xunit;

namespace GameInterface.Tests.Services.Voice;

public partial class VoiceAudioTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DecoderFailureDiscardsOnlyOffendingStreamAndPreservesPlaybackAndCapture(bool realCodec)
    {
        using var h = new WorkerHarness();
        var opus = new OpusVoiceCodecFactory();
        byte[] healthy = opus.CreateEncoder().Encode(Enumerable.Repeat((short)8000, 960).ToArray());
        byte[] invalid = new byte[] { 255 };
        if (realCodec)
        {
            // Valid Opus with a 10ms duration passes packet validation but violates our 20ms codec contract.
            var encoder = OpusEncoder.Create(48000, 1, OpusApplication.OPUS_APPLICATION_VOIP);
            invalid = new byte[VoicePacket.MaximumPayload];
            int length = encoder.Encode(new short[480], 0, 480, invalid, 0, invalid.Length);
            Array.Resize(ref invalid, length);
            var error = Assert.Throws<InvalidOperationException>(() => opus.CreateDecoder().Decode(invalid));
            Assert.Equal("Invalid voice frame duration.", error.Message);
        }
        int failures = 0;
        h.Codecs.Setup(x => x.CreateDecoder()).Returns(() =>
        {
            Interlocked.Increment(ref h.DecoderCount);
            var actual = opus.CreateDecoder();
            var decoder = new Mock<IVoiceDecoder>();
            decoder.Setup(x => x.Decode(It.IsAny<byte[]>())).Returns<byte[]>(bytes =>
            {
                try
                {
                    if (!realCodec && ReferenceEquals(bytes, invalid)) throw new InvalidOperationException("bad frame");
                    return actual.Decode(bytes);
                }
                catch
                {
                    Interlocked.Increment(ref failures);
                    throw;
                }
            });
            return decoder.Object;
        });
        void Receive(string speaker, uint sequence, byte[] bytes)
        {
            var packet = new VoicePacket
            {
                Position = new VoicePosition("campaign", 1, 0, 0, 0, true, true), ListenerEpoch = 1,
                Sequence = sequence, Audio = bytes, Speaker = speaker, Gain = 1
            };
            Assert.True(packet.IsValid);
            h.Audio.Receive(packet);
        }
        h.Start();
        Receive("bad", 1, healthy);
        Receive("healthy", 1, healthy);
        Wait(() => Volatile.Read(ref h.ReceivedCount) == 2);
        h.Advance(1060);
        Wait(() => h.Audio.AudibleSpeakers.Count == 2);
        Receive("bad", 2, invalid);
        Receive("bad", 3, invalid);
        Receive("healthy", 2, healthy);
        Receive("healthy", 3, healthy);
        Wait(() => Volatile.Read(ref h.ReceivedCount) == 6);
        h.Advance(1080);
        Wait(() => h.Played.Count == 2 && h.Audio.AudibleSpeakers.Count == 1);
        Assert.Equal("healthy", Assert.Single(h.Audio.AudibleSpeakers));
        h.Advance(1100);
        Wait(() => h.Played.Count == 3);
        h.Callbacks.Single().data(Pcm(8000), 1920);
        Wait(() => h.Encoded.Count == 1);
        Assert.Equal(1, Volatile.Read(ref failures));
        Assert.Equal(2, h.DecoderCount);
        Assert.Equal("Ready", h.Audio.Status);
        Assert.Equal(0, Volatile.Read(ref h.InputDisposals));
        Assert.Equal(1, Volatile.Read(ref h.Clears));
        h.Device.Verify(x => x.Dispose(), Times.Never);
        h.Output.Verify(x => x.Open(It.IsAny<Action<Exception>>()), Times.Once);
        Receive("bad", 4, healthy);
        Wait(() => Volatile.Read(ref h.ReceivedCount) == 7);
        Assert.Equal(3, h.DecoderCount);
        h.Advance(1160);
        Wait(() => h.Audio.AudibleSpeakers.Contains("bad"));
    }
}
