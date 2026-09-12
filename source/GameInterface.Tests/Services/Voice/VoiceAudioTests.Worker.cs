using Common.Voice;
using GameInterface.Services.Voice;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading;
using Xunit;

namespace GameInterface.Tests.Services.Voice;

public partial class VoiceAudioTests
{
    internal sealed class WorkerHarness : IDisposable
    {
        public readonly Mock<IVoiceDeviceFactory> Output = new();
        public readonly Mock<IVoiceDevice> Device = new();
        public readonly Mock<IVoiceCaptureFactory> Input = new();
        public readonly Mock<IVoiceCodecFactory> Codecs = new();
        public readonly ConcurrentQueue<(Action<byte[], int> data, Action<Exception> error)> Callbacks = new();
        public readonly ConcurrentQueue<byte[]> Played = new();
        public readonly ConcurrentQueue<(VoiceInputSnapshot snapshot, byte[] bytes)> Encoded = new();
        public readonly ConcurrentQueue<byte[]?> Decoded = new();
        public readonly VoiceAudio Audio;
        public VoiceSettings Options = new();
        public long Now = 1000;
        public int DecoderCount;
        public int ReceivedCount;
        public int InputDisposals;
        public int Clears;
        public Exception? OpenError;

        public WorkerHarness(bool realCodec = false, IVoicePolicy? policy = null)
        {
            Device.Setup(x => x.Play(It.IsAny<byte[]>())).Callback<byte[]>(Played.Enqueue);
            Device.Setup(x => x.Clear()).Callback(() => Interlocked.Increment(ref Clears));
            Output.Setup(x => x.Open(It.IsAny<Action<Exception>>())).Returns(Device.Object);
            Input.Setup(x => x.Open(It.IsAny<VoiceSettings>(), It.IsAny<Action<byte[], int>>(), It.IsAny<Action<Exception>>()))
                .Returns<VoiceSettings, Action<byte[], int>, Action<Exception>>((_, data, error) =>
                {
                    Callbacks.Enqueue((data, error));
                    if (OpenError != null) throw OpenError;
                    var capture = new Mock<IDisposable>();
                    capture.Setup(x => x.Dispose()).Callback(() => Interlocked.Increment(ref InputDisposals));
                    return capture.Object;
                });
            var opus = new OpusVoiceCodecFactory();
            Codecs.Setup(x => x.CreateEncoder()).Returns(() => opus.CreateEncoder());
            Codecs.Setup(x => x.CreateDecoder()).Returns(() =>
            {
                Interlocked.Increment(ref DecoderCount);
                if (realCodec) return opus.CreateDecoder();
                var decoder = new Mock<IVoiceDecoder>();
                decoder.Setup(x => x.Decode(It.IsAny<byte[]>())).Returns<byte[]>(bytes =>
                {
                    Decoded.Enqueue(bytes);
                    return Enumerable.Repeat((short)1000, 960).ToArray();
                });
                return decoder.Object;
            });
            var clock = new Mock<IVoiceClock>();
            clock.SetupGet(x => x.Milliseconds).Returns(() => Interlocked.Read(ref Now));
            Audio = new VoiceAudio(Codecs.Object, Output.Object, Input.Object, policy ?? new VoicePolicy(), clock.Object,
                () => new ObservedBuffer(() => Interlocked.Increment(ref ReceivedCount)));
            Audio.Encoded += (snapshot, bytes) => Encoded.Enqueue((snapshot, bytes));
        }

        private sealed class ObservedBuffer : IVoiceJitterBuffer
        {
            private readonly VoiceJitterBuffer buffer = new();
            private readonly Action added;
            public ObservedBuffer(Action added) { this.added = added; }
            public bool Add(uint sequence, byte[] bytes, long now)
            {
                bool result = buffer.Add(sequence, bytes, now);
                added();
                return result;
            }
            public bool TryRead(long now, out byte[] bytes) => buffer.TryRead(now, out bytes);
            public void Clear() => buffer.Clear();
        }

        public void Start()
        {
            Sample();
            Wait(() => Callbacks.Count == 1 && Volatile.Read(ref Clears) > 0);
        }

        public void Sample(long epoch = 1, bool ptt = true, bool focused = true, bool typing = false)
            => Audio.Update(new VoiceInputSnapshot(new VoicePosition("campaign", epoch, 0, 0, 0, true, true),
                focused, typing, ptt, Interlocked.Read(ref Now)), Options);

        public void Frame(uint sequence, long epoch = 1, string speaker = "speaker", byte value = 1)
            => Audio.Receive(new VoicePacket
            {
                Position = new VoicePosition("campaign", epoch, 0, 0, 0, true, true), ListenerEpoch = 1,
                Sequence = sequence, Audio = new[] { value }, Speaker = speaker, Gain = 1
            });

        public void Advance(long now)
        {
            Interlocked.Exchange(ref Now, now);
            Sample();
        }

        public void Dispose() => Audio.Dispose();
    }

    internal static void Wait(Func<bool> condition)
        => Assert.True(SpinWait.SpinUntil(condition, 3000), "Voice worker did not reach the expected state");

    internal static byte[] Pcm(short value)
    {
        var pcm = Enumerable.Repeat(value, 960).ToArray();
        var bytes = new byte[1920];
        Buffer.BlockCopy(pcm, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    [Fact]
    public void SustainedTwentyFiveMillisecondWakeupsKeepTwentyMillisecondPlaybackTimeline()
    {
        using var h = new WorkerHarness();
        h.Start();
        h.Frame(1);
        Wait(() => Volatile.Read(ref h.ReceivedCount) == 1);
        uint produced = 1;
        for (int elapsed = 25; elapsed <= 2000; elapsed += 25)
        {
            h.Advance(1000 + elapsed);
            while (produced <= elapsed / 20) h.Frame(++produced);
            Wait(() => Volatile.Read(ref h.ReceivedCount) == produced);
            int due = elapsed < 60 ? 0 : ((elapsed - 60) / 20) + 1;
            Wait(() => h.Played.Count >= due);
            Assert.Equal(due, h.Played.Count);
        }
        Assert.DoesNotContain(h.Decoded, bytes => bytes == null);
        Assert.Equal(1, h.DecoderCount);
    }

    [Fact]
    public void TenStreamsDropStalledQueuesAndResumeWithoutPlaybackBurst()
    {
        using var h = new WorkerHarness();
        h.Start();
        for (int speaker = 0; speaker < 10; speaker++) h.Frame(1, speaker: "speaker" + speaker);
        Wait(() => Volatile.Read(ref h.ReceivedCount) == 10);
        h.Advance(1060);
        Wait(() => h.Played.Count == 1);
        int clears = Volatile.Read(ref h.Clears);
        h.Advance(1600);
        Wait(() => Volatile.Read(ref h.Clears) > clears);
        Assert.Single(h.Played);
        for (int speaker = 0; speaker < 10; speaker++)
            for (uint frame = 2; frame <= 6; frame++) h.Frame(frame, speaker: "speaker" + speaker);
        Wait(() => Volatile.Read(ref h.ReceivedCount) == 60);
        h.Advance(1660);
        Wait(() => h.Played.Count == 2);
        Assert.DoesNotContain(h.Decoded, bytes => bytes == null);
    }

    [Fact]
    public void CatchupLimitResetsRemainingTimelineInsteadOfGrowingABacklog()
    {
        using var h = new WorkerHarness();
        h.Start();
        for (uint frame = 1; frame <= 5; frame++) h.Frame(frame);
        Wait(() => Volatile.Read(ref h.ReceivedCount) == 5);
        int clears = Volatile.Read(ref h.Clears);
        h.Advance(1090);
        Wait(() => Volatile.Read(ref h.Clears) > clears);
        Assert.InRange(h.Played.Count, 0, 3);
        int oldPlays = h.Played.Count;
        h.Frame(6);
        Wait(() => Volatile.Read(ref h.ReceivedCount) == 6);
        h.Advance(1150);
        Wait(() => h.Played.Count == oldPlays + 1);
        Assert.DoesNotContain(h.Decoded, bytes => bytes == null);
    }

    [Fact]
    public void OlderSpeakerEpochNeverReplacesDecoderEvenAfterEvictionAndRetry()
    {
        using var h = new WorkerHarness();
        h.Start();
        h.Frame(1, epoch: 3, value: 3);
        Wait(() => Volatile.Read(ref h.ReceivedCount) == 1);
        h.Frame(2, epoch: 1);
        h.Frame(2, epoch: 3, value: 3);
        Wait(() => Volatile.Read(ref h.ReceivedCount) >= 2);
        Assert.Equal(1, h.DecoderCount);
        h.Advance(1060);
        Wait(() => h.Played.Count == 1);
        h.Advance(1500);
        Wait(() => Volatile.Read(ref h.Clears) >= 2);
        h.Audio.Retry();
        Wait(() => h.Callbacks.Count == 2);
        h.Frame(3, epoch: 1);
        h.Frame(3, epoch: 3, value: 3);
        Wait(() => Volatile.Read(ref h.ReceivedCount) >= 3);
        Assert.Equal(2, h.DecoderCount);
        h.Advance(1560);
        Wait(() => h.Played.Count == 2);
        Assert.All(h.Decoded, bytes => Assert.Equal(new byte[] { 3 }, bytes));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MicrophoneFailurePreservesReceivingAndRetryDoesNotReopenOutput(bool asynchronous)
    {
        using var h = new WorkerHarness();
        if (!asynchronous) h.OpenError = new InvalidOperationException("missing microphone");
        h.Start();
        if (asynchronous) h.Callbacks.First().error(new InvalidOperationException("unplugged microphone"));
        Wait(() => h.Audio.Status.Contains("Microphone unavailable"));
        h.Frame(1);
        Wait(() => Volatile.Read(ref h.ReceivedCount) == 1);
        h.Advance(1060);
        Wait(() => h.Played.Count == 1);
        h.Device.Verify(x => x.Dispose(), Times.Never);
        h.OpenError = null;
        h.Audio.Retry();
        Wait(() => h.Callbacks.Count == 2 && h.Audio.Status == "Ready");
        h.Output.Verify(x => x.Open(It.IsAny<Action<Exception>>()), Times.Once);
        h.Callbacks.First().error(new InvalidOperationException("old capture"));
        Assert.Equal("Ready", h.Audio.Status);
        h.Callbacks.Last().data(Pcm(8000), 1920);
        Wait(() => h.Encoded.Count == 1);
        h.Audio.Dispose();
        h.Device.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public void RealCaptureUsesSensitivityAndMicrophoneTestPlaysLocallyWithoutEncoding()
    {
        using var h = new WorkerHarness(realCodec: true);
        h.Options = new VoiceSettings { Activation = VoiceActivation.VoiceActivity, Sensitivity = 0.1f };
        h.Start();
        h.Callbacks.Last().data(Pcm(100), 1920);
        h.Callbacks.Last().data(Pcm(8000), 1920);
        Wait(() => h.Encoded.Count == 1);
        Assert.Equal(960, new OpusVoiceCodecFactory().CreateDecoder().Decode(h.Encoded.Single().bytes).Length);
        h.Audio.TestMicrophone(true);
        Wait(() => h.Callbacks.Count == 2 && Volatile.Read(ref h.Clears) >= 2);
        h.Callbacks.Last().data(Pcm(100), 1920);
        Wait(() => h.Played.Count == 1);
        Assert.Equal(Pcm(100), h.Played.Single());
        Assert.Single(h.Encoded);
        h.Audio.TestMicrophone(false);
        Wait(() => h.Callbacks.Count == 3 && Volatile.Read(ref h.Clears) >= 3);
        h.Sample(ptt: true, focused: false);
        h.Callbacks.Last().data(Pcm(8000), 1920);
        h.Sample(typing: true);
        h.Callbacks.Last().data(Pcm(8000), 1920);
        h.Sample();
        h.Callbacks.Last().data(Pcm(8000), 1920);
        Wait(() => h.Encoded.Count == 2);
        Assert.Single(h.Played);
    }

    [Fact]
    public void BlockedCaptureFailureCannotFaultChangedContext()
    {
        using var h = new WorkerHarness();
        h.Start();
        var callback = h.Callbacks.First().error;
        var error = new Mock<Exception>();
        error.SetupGet(x => x.Message).Returns("retired device");
        var exception = error.Object;
        object gate = typeof(VoiceAudio).GetField("gate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(h.Audio)!;
        var thread = new Thread(() => callback(exception));
        lock (gate)
        {
            thread.Start();
            Wait(() => (thread.ThreadState & ThreadState.WaitSleepJoin) != 0);
            h.Sample(epoch: 2);
        }
        Assert.True(thread.Join(3000));
        Wait(() => h.Callbacks.Count == 2);
        error.VerifyGet(x => x.Message, Times.Never);
        Assert.Equal("Ready", h.Audio.Status);
    }

    [Fact]
    public void PendingCaptureQueueIsBoundedAndReleasedPttFramesAreNotEncoded()
    {
        using var h = new WorkerHarness();
        h.Start();
        object gate = typeof(VoiceAudio).GetField("gate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(h.Audio)!;
        lock (gate)
        {
            for (int i = 0; i < 20; i++) h.Callbacks.Last().data(Pcm(8000), 1920);
        }
        Wait(() => h.Encoded.Count == 3);
        lock (gate)
        {
            h.Sample(ptt: false);
            h.Callbacks.Last().data(Pcm(8000), 1920);
            h.Sample();
            h.Callbacks.Last().data(Pcm(8000), 1920);
        }
        Wait(() => h.Encoded.Count == 4);
    }

    [Fact]
    public void CaptureBlockedAtGateCannotAdoptReplacementContext()
    {
        var policy = new Mock<IVoicePolicy>();
        using var h = new WorkerHarness(policy: policy.Object);
        h.Start();
        var callback = h.Callbacks.First().data;
        // Hold the actual callback gate to force the pre-lock validation race from the review.
        object gate = typeof(VoiceAudio).GetField("gate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(h.Audio)!;
        var thread = new Thread(() => callback(Pcm(8000), 1920));
        lock (gate)
        {
            thread.Start();
            Wait(() => (thread.ThreadState & ThreadState.WaitSleepJoin) != 0);
            h.Sample(epoch: 2);
        }
        Assert.True(thread.Join(3000));
        Wait(() => h.Callbacks.Count == 2);
        policy.Verify(x => x.CanTransmit(It.IsAny<VoiceActivation>(), It.IsAny<bool>(), It.IsAny<bool>(),
            It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<float>(), It.IsAny<float>()), Times.Never);
        Assert.Empty(h.Encoded);
    }
}
