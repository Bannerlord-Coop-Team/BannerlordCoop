using GameInterface.Services.Voice;
using Moq;
using NAudio.Wave;
using System;
using System.Threading.Tasks;
using Xunit;

namespace GameInterface.Tests.Services.Voice;

/// <summary>Exercises microphone teardown without opening a native recording device.</summary>
public class WindowsVoiceCaptureFactoryTests
{
    [Fact]
    // Native buffers remain owned until completion, even when disposal is repeated.
    public void StopAsync_DefersReleaseUntilRecordingStopped_AndReleasesOnce()
    {
        var input = NewRecordingInput();
        var capture = new WindowsVoiceCaptureFactory.Capture(input.Object, (_, _) => { }, _ => { });

        var completion = capture.StopAsync();
        Assert.Same(completion, capture.StopAsync());
        Assert.False(completion.IsCompleted);

        input.Verify(x => x.StopRecording(), Times.Once);
        input.Verify(x => x.Dispose(), Times.Never);
        input.Raise(x => x.RecordingStopped += null, new StoppedEventArgs());
        input.Raise(x => x.RecordingStopped += null, new StoppedEventArgs());
        Assert.Same(completion, capture.StopAsync());
        Assert.True(completion.IsCompletedSuccessfully);
        input.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    // An inline completion must not close the handle while StopRecording still uses it.
    public void StopAsync_InlineStoppedEvent_DoesNotReleaseUntilStopReturns()
    {
        var input = NewRecordingInput();
        input.Setup(x => x.StopRecording()).Callback(() =>
        {
            input.Raise(x => x.RecordingStopped += null, new StoppedEventArgs());
            input.Verify(x => x.Dispose(), Times.Never);
        });
        var capture = new WindowsVoiceCaptureFactory.Capture(input.Object, (_, _) => { }, _ => { });

        capture.StopAsync();

        input.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    // A completion on another thread must be able to return before StopRecording returns.
    public void StopAsync_ConcurrentStoppedEvent_DoesNotHoldLockAcrossStop()
    {
        var input = NewRecordingInput();
        bool eventReturned = false;
        input.Setup(x => x.StopRecording()).Callback(() =>
        {
            var callback = Task.Run(() => input.Raise(x => x.RecordingStopped += null, new StoppedEventArgs()));
            eventReturned = callback.Wait(TimeSpan.FromSeconds(5));
            input.Verify(x => x.Dispose(), Times.Never);
        });
        var capture = new WindowsVoiceCaptureFactory.Capture(input.Object, (_, _) => { }, _ => { });

        capture.StopAsync();

        Assert.True(eventReturned, "RecordingStopped was blocked by StopRecording's lock");
        input.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    // An early logical dispose must not let NAudio overwrite the native stop request during startup.
    public void StopAsync_BeforeFirstData_StopsOnlyAfterWorkerHasStartedWithoutPublishingAudio()
    {
        var input = new Mock<IWaveIn>();
        int frames = 0;
        int failures = 0;
        var capture = new WindowsVoiceCaptureFactory.Capture(input.Object, (_, _) => frames++, _ => failures++);

        capture.StopAsync();
        input.Verify(x => x.StopRecording(), Times.Never);
        input.Verify(x => x.Dispose(), Times.Never);
        input.Raise(x => x.DataAvailable += null, new WaveInEventArgs(new byte[1920], 1920));
        input.Raise(x => x.DataAvailable += null, new WaveInEventArgs(new byte[1920], 1920));

        input.Verify(x => x.StopRecording(), Times.Once);
        input.Verify(x => x.Dispose(), Times.Never);
        input.Raise(x => x.RecordingStopped += null, new StoppedEventArgs(new InvalidOperationException()));
        Assert.Equal(0, frames);
        Assert.Equal(0, failures);
        input.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    // A worker failure releases resources and reports the original capture error.
    public void RecordingStopped_ReportsFailureAndReleasesWithoutAnotherStop()
    {
        var input = NewRecordingInput();
        var failure = new InvalidOperationException("recording failed");
        Exception? reported = null;
        var capture = new WindowsVoiceCaptureFactory.Capture(input.Object, (_, _) => { }, error => reported = error);

        input.Raise(x => x.RecordingStopped += null, new StoppedEventArgs(failure));
        capture.StopAsync();

        Assert.Same(failure, reported);
        input.Verify(x => x.StopRecording(), Times.Never);
        input.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    // Failed startup has no recording worker to wait for.
    public void StartRecordingFailure_ReleasesWithoutWaitingForAnUnscheduledWorker()
    {
        var input = NewRecordingInput();
        input.Setup(x => x.StartRecording()).Throws(new InvalidOperationException("open failed"));

        Assert.Throws<InvalidOperationException>(() =>
            new WindowsVoiceCaptureFactory.Capture(input.Object, (_, _) => { }, _ => { }));

        input.Verify(x => x.StopRecording(), Times.Never);
        input.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    // Early completion cannot release resources while StartRecording is still running.
    public void StartRecording_InlineCompletion_DefersReleaseUntilStartReturns()
    {
        var input = NewRecordingInput();
        input.Setup(x => x.StartRecording()).Callback(() =>
        {
            input.Raise(x => x.RecordingStopped += null, new StoppedEventArgs());
            input.Verify(x => x.Dispose(), Times.Never);
        });

        var capture = new WindowsVoiceCaptureFactory.Capture(input.Object, (_, _) => { }, _ => { });

        input.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    // Pending old capture callbacks cannot affect the replacement capture.
    public void ReplacementCapture_OldCompletionDoesNotReleaseOrPublishIntoReplacement()
    {
        var oldInput = new Mock<IWaveIn>();
        var newInput = new Mock<IWaveIn>();
        int oldFrames = 0;
        int newFrames = 0;
        int failures = 0;
        var oldCapture = new WindowsVoiceCaptureFactory.Capture(oldInput.Object, (_, _) => oldFrames++, _ => failures++);
        oldCapture.StopAsync();
        var newCapture = new WindowsVoiceCaptureFactory.Capture(newInput.Object, (_, _) => newFrames++, _ => failures++);

        oldInput.Raise(x => x.DataAvailable += null, new WaveInEventArgs(new byte[1920], 1920));
        oldInput.Raise(x => x.RecordingStopped += null, new StoppedEventArgs(new InvalidOperationException()));
        newInput.Raise(x => x.DataAvailable += null, new WaveInEventArgs(new byte[1920], 1920));

        Assert.Equal(0, oldFrames);
        Assert.Equal(1, newFrames);
        Assert.Equal(0, failures);
        oldInput.Verify(x => x.Dispose(), Times.Once);
        newInput.Verify(x => x.Dispose(), Times.Never);
        newInput.Verify(x => x.StopRecording(), Times.Never);
        newCapture.StopAsync();
        newInput.Raise(x => x.RecordingStopped += null, new StoppedEventArgs());
        newInput.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    // A failed stop cannot make native release safe before worker completion.
    public void StopRecordingFailure_DoesNotFreeBuffersBeforeWorkerCompletion()
    {
        var input = NewRecordingInput();
        input.Setup(x => x.StopRecording()).Throws(new InvalidOperationException("stop failed"));
        var capture = new WindowsVoiceCaptureFactory.Capture(input.Object, (_, _) => { }, _ => { });

        var completion = capture.StopAsync();

        Assert.False(completion.IsCompleted);
        input.Verify(x => x.Dispose(), Times.Never);
        input.Raise(x => x.RecordingStopped += null, new StoppedEventArgs());
        Assert.True(completion.IsCompletedSuccessfully);
        input.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    // Completion is a release guarantee, not merely the RecordingStopped notification.
    public void StopAsync_CompletesOnlyAfterNativeDisposeReturns()
    {
        var input = NewRecordingInput();
        var capture = new WindowsVoiceCaptureFactory.Capture(input.Object, (_, _) => { }, _ => { });
        var completion = capture.StopAsync();
        input.Setup(x => x.Dispose()).Callback(() => Assert.False(completion.IsCompleted));

        input.Raise(x => x.RecordingStopped += null, new StoppedEventArgs());

        Assert.True(completion.IsCompletedSuccessfully);
    }

    [Fact]
    // A release failure must never be mistaken for permission to open a replacement.
    public async Task StopAsync_NativeReleaseFailureFaultsStableCompletion()
    {
        var input = NewRecordingInput();
        var failure = new InvalidOperationException("release failed");
        input.Setup(x => x.Dispose()).Throws(failure);
        var capture = new WindowsVoiceCaptureFactory.Capture(input.Object, (_, _) => { }, _ => { });
        var completion = capture.StopAsync();

        input.Raise(x => x.RecordingStopped += null, new StoppedEventArgs());

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => completion));
        Assert.Same(completion, capture.StopAsync());
        input.Verify(x => x.Dispose(), Times.Once);
    }

    // Most cases begin with a worker that has already delivered its first buffer.
    private static Mock<IWaveIn> NewRecordingInput()
    {
        var input = new Mock<IWaveIn>();
        input.Setup(x => x.StartRecording()).Callback(() =>
            input.Raise(x => x.DataAvailable += null, new WaveInEventArgs(new byte[1920], 1920)));
        return input;
    }

}
