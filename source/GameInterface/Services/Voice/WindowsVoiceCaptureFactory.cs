using NAudio.Wave;
using System;
using System.Collections.Generic;

namespace GameInterface.Services.Voice;

public interface IVoiceCaptureFactory
{
    IReadOnlyList<string> Microphones();
    IDisposable Open(VoiceSettings settings, Action<byte[], int> captured, Action<Exception> failed);
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
    public IDisposable Open(VoiceSettings settings, Action<byte[], int> captured, Action<Exception> failed)
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

    /// <summary>Defers native release until recording and any in-flight control call have finished.</summary>
    internal sealed class Capture : IDisposable
    {
        private readonly IWaveIn input;
        private readonly Action<byte[], int> captured;
        private readonly Action<Exception> failed;
        private readonly object gate = new();
        private volatile bool disposed;
        private bool captureStarted;
        private bool stopRequested;
        private bool recordingStopped;
        private bool controlCall = true;
        private bool released;

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
                lock (gate) { disposed = recordingStopped = true; }
                throw;
            }
            finally { CompleteControlCall(); }
        }

        /// <summary>Suppresses old capture callbacks without waiting on the recording thread.</summary>
        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
            }
            RequestStop();
        }

        // Data proves NAudio's worker has passed its unconditional initial Capturing assignment.
        private void OnDataAvailable(object sender, WaveInEventArgs args)
        {
            lock (gate) captureStarted = true;
            if (disposed) { RequestStop(); return; }
            captured(args.Buffer, args.BytesRecorded);
        }

        // RecordingStopped is raised after DoRecording's final buffer use, possibly on a captured context.
        private void OnRecordingStopped(object sender, StoppedEventArgs args)
        {
            lock (gate) recordingStopped = true;
            ReleaseIfStopped();
            if (!disposed && args.Exception != null) failed(args.Exception);
        }

        // Do not hold gate across NAudio calls or close a handle while StopRecording still uses it.
        private void RequestStop()
        {
            lock (gate)
            {
                if (!captureStarted || stopRequested || recordingStopped || controlCall || released) return;
                stopRequested = true;
                controlCall = true;
            }
            try { input.StopRecording(); }
            finally { CompleteControlCall(); }
        }

        // An inline or concurrent RecordingStopped callback must wait for the control call to return.
        private void CompleteControlCall()
        {
            lock (gate) controlCall = false;
            ReleaseIfStopped();
            if (disposed) RequestStop();
        }

        // Claim release once under gate, but unsubscribe and dispose outside it.
        private void ReleaseIfStopped()
        {
            lock (gate)
            {
                if (!recordingStopped || controlCall || released) return;
                released = true;
            }
            input.DataAvailable -= OnDataAvailable;
            input.RecordingStopped -= OnRecordingStopped;
            input.Dispose();
        }
    }
}
