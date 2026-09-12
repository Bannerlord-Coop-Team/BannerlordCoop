using Common.Voice;
using GameInterface.Services.Voice;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;

namespace GameInterface.Tests.Services.Voice;

public partial class VoiceAudioTests
{
    // NAudio copies provider samples into separate device buffers before waveOutWrite.
    private sealed class BufferedOutput : IVoiceDevice
    {
        private readonly object gate = new();
        private readonly Queue<byte[]> provider = new();
        private readonly Queue<byte[]> hardware = new();
        private readonly Action cleared;
        private readonly Action<Exception> failed;
        private bool disposed;

        public BufferedOutput(Action cleared, Action<Exception> failed)
        {
            this.cleared = cleared;
            this.failed = failed;
        }

        public int ProviderCount { get { lock (gate) return provider.Count; } }
        public int HardwareCount { get { lock (gate) return hardware.Count; } }
        public bool IsDisposed { get { lock (gate) return disposed; } }
        public void Play(byte[] pcm) { lock (gate) provider.Enqueue(pcm); }
        public void SubmitToHardware() { lock (gate) hardware.Enqueue(provider.Dequeue()); }
        public void RaiseRetiredFailure() => failed(new InvalidOperationException("retired output"));

        public void Clear()
        {
            lock (gate) provider.Clear();
            cleared();
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
                provider.Clear();
                hardware.Clear();
            }
            // Shutdown must not hold VoiceAudio's state gate while a device callback needs it.
            var callback = new Thread(RaiseRetiredFailure);
            callback.Start();
            Assert.True(callback.Join(3000), "Output shutdown blocked its callback");
        }
    }

    private ConcurrentQueue<BufferedOutput> UseBufferedOutputs(WorkerHarness harness)
    {
        var outputs = new ConcurrentQueue<BufferedOutput>();
        harness.Output.Setup(x => x.Open(It.IsAny<Action<Exception>>())).Returns<Action<Exception>>(failed =>
        {
            var output = new BufferedOutput(() => Interlocked.Increment(ref harness.Clears), failed);
            outputs.Enqueue(output);
            return output;
        });
        return outputs;
    }

    private void QueueRemoteAudioInProviderAndHardware(WorkerHarness harness, BufferedOutput output)
    {
        harness.Frame(1);
        harness.Frame(2);
        Wait(() => Volatile.Read(ref harness.ReceivedCount) == 2);
        harness.Advance(1060);
        Wait(() => output.ProviderCount == 1);
        output.SubmitToHardware();
        harness.Advance(1080);
        Wait(() => output.ProviderCount == 1);
        Assert.Equal(1, output.HardwareCount);
    }

    [Theory]
    [InlineData("context")]
    [InlineData("deafen")]
    [InlineData("stop-test")]
    public void ExplicitResetDiscardsProviderAndAlreadySubmittedOutput(string transition)
    {
        using var harness = new WorkerHarness();
        var outputs = UseBufferedOutputs(harness);
        harness.Start();
        if (transition == "stop-test")
        {
            int initialClears = Volatile.Read(ref harness.Clears);
            harness.Audio.TestMicrophone(true);
            Wait(() => harness.Callbacks.Count == 2 && Volatile.Read(ref harness.Clears) > initialClears);
        }
        var oldOutput = outputs.Last();
        if (transition == "stop-test")
        {
            harness.Callbacks.Last().data(Pcm(8000), 1920);
            Wait(() => oldOutput.ProviderCount == 1);
            oldOutput.SubmitToHardware();
            harness.Callbacks.Last().data(Pcm(8000), 1920);
            Wait(() => oldOutput.ProviderCount == 1);
            Assert.Empty(harness.Encoded);
        }
        else QueueRemoteAudioInProviderAndHardware(harness, oldOutput);
        Assert.Equal(1, oldOutput.HardwareCount);
        int callbacks = harness.Callbacks.Count;
        int clears = Volatile.Read(ref harness.Clears);
        int opens = outputs.Count;
        if (transition == "stop-test") harness.Audio.TestMicrophone(false);
        else
        {
            bool hear = transition != "deafen";
            harness.Options = new VoiceSettings { Deafened = !hear };
            harness.Audio.Update(new VoiceInputSnapshot(new VoicePosition(
                hear ? "battle:new" : "campaign", 2, 0, 0, 0, hear, hear),
                true, false, true, harness.Now), harness.Options);
        }
        Wait(() => harness.Callbacks.Count == callbacks + 1 && Volatile.Read(ref harness.Clears) > clears);
        Assert.Equal(0, oldOutput.ProviderCount);
        Assert.Equal(0, oldOutput.HardwareCount);
        Assert.True(oldOutput.IsDisposed);
        Assert.Equal(opens + 1, outputs.Count);
        Assert.Equal(0, outputs.Last().HardwareCount);
        Assert.Equal(0, outputs.Last().ProviderCount);
        oldOutput.RaiseRetiredFailure();
        Assert.Equal("Ready", harness.Audio.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MicrophoneRetryPreservesHealthyOutputAndSubmittedSamples(bool captureFailed)
    {
        using var harness = new WorkerHarness();
        var outputs = UseBufferedOutputs(harness);
        harness.Start();
        var output = outputs.Single();
        QueueRemoteAudioInProviderAndHardware(harness, output);
        if (captureFailed)
        {
            harness.Callbacks.First().error(new InvalidOperationException("microphone unplugged"));
            Wait(() => Volatile.Read(ref harness.InputDisposals) == 1);
            Assert.Contains("Microphone unavailable", harness.Audio.Status);
        }
        harness.Audio.Retry();
        Wait(() => harness.Callbacks.Count == 2 && harness.Audio.Status == "Ready");
        Assert.Single(outputs);
        Assert.False(output.IsDisposed);
        Assert.Equal(1, output.HardwareCount);
        Assert.Equal(1, output.ProviderCount);
    }
}
