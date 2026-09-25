using Common.Logging;
using NAudio.Wave;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GameInterface.Services.Voice;

/// <summary>Owns one microphone until its asynchronous native release completes.</summary>
public interface IVoiceCapture
{
    /// <summary>Suppresses capture immediately; succeeds only after native release, faults if release fails.</summary>
    /// <remarks>Repeated calls return the same task; no data or completion can leave it pending indefinitely.</remarks>
    Task StopAsync();
}

public interface IVoiceCaptureFactory
{
    IReadOnlyList<string> Microphones();
    IVoiceCapture Open(VoiceSettings settings, Action<byte[], int> captured, Action<Exception> failed);
}

public sealed class WindowsVoiceCaptureFactory : IVoiceCaptureFactory
{
    public IReadOnlyList<string> Microphones()
    {
        var names = new List<string> { "System default" };
        for (int i = 0; i < WaveInEvent.DeviceCount; i++) names.Add(WaveInEvent.GetCapabilities(i).ProductName);
        return names;
    }

    /// <summary>Defers microphone resource release until recording has stopped.</summary>
    public IVoiceCapture Open(VoiceSettings settings, Action<byte[], int> captured, Action<Exception> failed)
    {
        int device = settings.Microphone;
        if (device >= WaveInEvent.DeviceCount || (device >= 0 && !string.IsNullOrEmpty(settings.MicrophoneName) &&
            settings.MicrophoneName != WaveInEvent.GetCapabilities(device).ProductName))
            throw new InvalidOperationException("Selected microphone is unavailable. Select it again and retry.");
        return new Capture(new WaveInEvent
        {
            DeviceNumber = device,
            WaveFormat = new WaveFormat(48000, 16, 1),
            BufferMilliseconds = 20,
            NumberOfBuffers = 3
        }, captured, failed);
    }

    /// <summary>Serializes stop and release without waiting for the recording worker.</summary>
    internal sealed class Capture : IVoiceCapture
    {
        private enum CaptureState { AwaitingData, Recording, Stopping, Stopped, Releasing, ReleaseFinished }

        private static readonly ILogger Logger = LogManager.GetLogger<Capture>();
        private readonly IWaveIn input;
        private readonly Action<byte[], int> captured;
        private readonly Action<Exception> failed;
        private readonly object gate = new();
        private readonly TaskCompletionSource<object> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private CaptureState state;
        private bool stopRequested;
        private bool nativeCallActive = true;

        /// <summary>Subscribes before starting so even immediate completion is observed.</summary>
        internal Capture(IWaveIn input, Action<byte[], int> captured, Action<Exception> failed)
        {
            this.input = input;
            this.captured = captured;
            this.failed = failed;
            input.DataAvailable += OnDataAvailable;
            input.RecordingStopped += OnRecordingStopped;
            try { input.StartRecording(); }
            catch
            {
                // WaveInEvent queues its recording worker only after all fallible startup calls.
                lock (gate) { stopRequested = true; state = CaptureState.Stopped; }
                throw;
            }
            finally
            {
                lock (gate) nativeCallActive = false;
                AdvanceShutdown();
            }
        }

        /// <summary>Returns the same release task on every request, without waiting on callbacks.</summary>
        public Task StopAsync()
        {
            lock (gate) stopRequested = true;
            AdvanceShutdown();
            return completion.Task;
        }

        // Data proves NAudio's worker has passed its unconditional initial Capturing assignment.
        private void OnDataAvailable(object sender, WaveInEventArgs args)
        {
            bool publish;
            lock (gate)
            {
                if (state == CaptureState.AwaitingData) state = CaptureState.Recording;
                publish = !stopRequested && state == CaptureState.Recording;
            }
            if (publish) captured(args.Buffer, args.BytesRecorded);
            else AdvanceShutdown();
        }

        // RecordingStopped follows DoRecording's final buffer use, possibly on a captured context.
        private void OnRecordingStopped(object sender, StoppedEventArgs args)
        {
            bool report;
            lock (gate)
            {
                if (state == CaptureState.Releasing || state == CaptureState.ReleaseFinished) return;
                state = CaptureState.Stopped;
                report = !stopRequested && args.Exception != null;
            }
            try { if (report) failed(args.Exception); }
            finally { AdvanceShutdown(); }
        }

        // One driver claims native work under gate and executes it outside gate; callbacks only advance state.
        private void AdvanceShutdown()
        {
            while (true)
            {
                bool release;
                lock (gate)
                {
                    if (nativeCallActive) return;
                    release = state == CaptureState.Stopped;
                    if (release) state = CaptureState.Releasing;
                    else if (stopRequested && state == CaptureState.Recording) state = CaptureState.Stopping;
                    else return;
                    nativeCallActive = true;
                }

                try
                {
                    if (release)
                    {
                        input.DataAvailable -= OnDataAvailable;
                        input.RecordingStopped -= OnRecordingStopped;
                        input.Dispose();
                        completion.TrySetResult(null);
                    }
                    else input.StopRecording();
                }
                catch (Exception ex)
                {
                    if (release) completion.TrySetException(ex);
                    else Logger.Warning(ex, "Voice microphone stop failed; waiting for recording completion");
                }
                finally
                {
                    lock (gate)
                    {
                        nativeCallActive = false;
                        if (release) state = CaptureState.ReleaseFinished;
                    }
                }
            }
        }
    }
}
