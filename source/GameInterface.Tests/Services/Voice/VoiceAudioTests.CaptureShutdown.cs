using GameInterface.Services.Voice;
using Moq;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace GameInterface.Tests.Services.Voice;

public partial class VoiceAudioTests
{
    [Fact]
    // Pending capture release must block replacement without interrupting received voice.
    public void Retry_WaitsForNativeReleaseWhilePlaybackContinues()
    {
        var release = new TaskCompletionSource<object>();
        using var h = new WorkerHarness { CaptureRelease = release.Task };
        h.Start();
        var old = h.Callbacks.Single();

        h.Audio.Retry();
        Wait(() => Volatile.Read(ref h.InputStopRequests) == 1);
        h.Frame(1);
        Wait(() => Volatile.Read(ref h.ReceivedCount) == 1);
        h.Advance(1060);
        Wait(() => h.Played.Count == 1);
        Assert.Single(h.Callbacks);
        old.data(Pcm(8000), 1920);
        old.error(new InvalidOperationException("retired capture"));
        Assert.Empty(h.Encoded);

        release.SetResult(null!);
        Wait(() => h.Callbacks.Count == 2);
        Assert.Equal(1, Volatile.Read(ref h.InputStopRequests));
        h.Output.Verify(x => x.Open(It.IsAny<Action<Exception>>()), Times.Once);
    }

    [Fact]
    // Repeated changes during shutdown coalesce into the latest microphone and callback generation.
    public void PendingStop_OpensOnlyLatestRequestedMicrophoneAndContext()
    {
        var release = new TaskCompletionSource<object>();
        using var h = new WorkerHarness { CaptureRelease = release.Task };
        h.Start();
        var old = h.Callbacks.Single();
        h.Audio.Retry();
        Wait(() => Volatile.Read(ref h.InputStopRequests) == 1);
        h.Options = new VoiceSettings { Microphone = 1 };
        h.Sample(epoch: 2);
        h.Options = new VoiceSettings { Microphone = 2 };
        h.Sample(epoch: 3);

        release.SetResult(null!);
        Wait(() => h.Callbacks.Count == 2);
        h.Input.Verify(x => x.Open(It.Is<VoiceSettings>(s => s.Microphone == 2),
            It.IsAny<Action<byte[], int>>(), It.IsAny<Action<Exception>>()), Times.Once);
        old.error(new InvalidOperationException("old capture"));
        old.data(Pcm(8000), 1920);
        h.Callbacks.Last().data(Pcm(8000), 1920);
        Wait(() => h.Encoded.Count == 1);
        Assert.Equal(3, h.Encoded.Single().snapshot.Position.Epoch);
        Assert.Equal("Ready", h.Audio.Status);
    }

    [Fact]
    // Disabling capture cancels a pending reopen even if release completes later.
    public void DisabledDuringStop_DoesNotReopenOnCompletion()
    {
        var release = new TaskCompletionSource<object>();
        using var h = new WorkerHarness { CaptureRelease = release.Task };
        h.Start();
        h.Audio.Retry();
        Wait(() => Volatile.Read(ref h.InputStopRequests) == 1);
        h.Options = new VoiceSettings { Enabled = false };
        h.Sample();
        Wait(() => h.Audio.Status == "Disabled");

        release.SetResult(null!);
        int codecCalls = h.Codecs.Invocations.Count;
        h.Sample(epoch: 2);
        Wait(() => h.Codecs.Invocations.Count > codecCalls);

        Assert.Single(h.Callbacks);
        Assert.Equal(1, Volatile.Read(ref h.InputStopRequests));
    }

    [Fact]
    // A faulted release is not permission to abandon the old device and open another one.
    public void NativeReleaseFailure_BlocksReplacementIncludingRetry()
    {
        var release = new TaskCompletionSource<object>();
        using var h = new WorkerHarness { CaptureRelease = release.Task };
        h.Start();
        h.Audio.Retry();
        Wait(() => Volatile.Read(ref h.InputStopRequests) == 1);

        release.SetException(new InvalidOperationException("native release failed"));
        Wait(() => h.Audio.Status.Contains("native release failed"));
        h.Audio.Retry();
        h.Frame(1);
        Wait(() => Volatile.Read(ref h.ReceivedCount) == 1);
        h.Advance(1060);
        Wait(() => h.Played.Count == 1);

        Assert.Single(h.Callbacks);
        Assert.Equal(1, Volatile.Read(ref h.InputStopRequests));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    // Final owner shutdown requests/observes capture release without waiting or reopening after it exits.
    public async Task FinalShutdown_DoesNotWaitForCaptureCompletion(bool replacementAlreadyPending)
    {
        var release = new TaskCompletionSource<object>();
        using var h = new WorkerHarness { CaptureRelease = release.Task };
        h.Start();
        if (replacementAlreadyPending)
        {
            h.Audio.Retry();
            Wait(() => Volatile.Read(ref h.InputStopRequests) == 1);
        }

        await Task.Run(h.Audio.Dispose).WaitAsync(TimeSpan.FromSeconds(3));

        Assert.False(release.Task.IsCompleted);
        Assert.Equal(1, Volatile.Read(ref h.InputStopRequests));
        h.Device.Verify(x => x.Dispose(), Times.Once);
        release.SetResult(null!);
        Assert.Single(h.Callbacks);
    }
}
