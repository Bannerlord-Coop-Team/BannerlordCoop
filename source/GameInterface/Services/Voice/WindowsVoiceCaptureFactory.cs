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

    public IDisposable Open(VoiceSettings settings, Action<byte[], int> captured, Action<Exception> failed)
        => new Capture(settings, captured, failed);

    private sealed class Capture : IDisposable
    {
        private readonly WaveInEvent input;
        private volatile bool disposed;

        public Capture(VoiceSettings settings, Action<byte[], int> captured, Action<Exception> failed)
        {
            int device = settings.Microphone;
            if (device >= WaveInEvent.DeviceCount || (device >= 0 && !string.IsNullOrEmpty(settings.MicrophoneName) &&
                settings.MicrophoneName != WaveInEvent.GetCapabilities(device).ProductName))
                throw new InvalidOperationException("Selected microphone is unavailable. Select it again and retry.");
            input = new WaveInEvent
            {
                DeviceNumber = device,
                WaveFormat = new WaveFormat(48000, 16, 1),
                BufferMilliseconds = 20,
                NumberOfBuffers = 3
            };
            input.DataAvailable += (_, args) => { if (!disposed) captured(args.Buffer, args.BytesRecorded); };
            input.RecordingStopped += (_, args) => { if (!disposed && args.Exception != null) failed(args.Exception); };
            try { input.StartRecording(); }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { input.StopRecording(); }
            finally { input.Dispose(); }
        }
    }
}
