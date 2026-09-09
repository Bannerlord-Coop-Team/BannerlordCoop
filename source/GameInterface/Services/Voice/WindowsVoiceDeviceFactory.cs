using NAudio.Wave;
using System;

namespace GameInterface.Services.Voice;

public interface IVoiceDeviceFactory
{
    IVoiceDevice Open(Action<Exception> failed);
}

public sealed class WindowsVoiceDeviceFactory : IVoiceDeviceFactory
{
    public IVoiceDevice Open(Action<Exception> failed) => new Device(failed);

    private sealed class Device : IVoiceDevice
    {
        private readonly WaveOutEvent output;
        private readonly BufferedWaveProvider playback;
        private volatile bool disposed;

        public Device(Action<Exception> failed)
        {
            try
            {
                playback = new BufferedWaveProvider(new WaveFormat(48000, 16, 1))
                {
                    BufferDuration = TimeSpan.FromMilliseconds(200),
                    DiscardOnBufferOverflow = true,
                    ReadFully = true
                };
                output = new WaveOutEvent { DesiredLatency = 60, NumberOfBuffers = 3 };
                output.PlaybackStopped += (_, args) => { if (!disposed && args.Exception != null) failed(args.Exception); };
                output.Init(playback);
                output.Play();
            }
            catch { Dispose(); throw; }
        }

        public void Play(byte[] pcm)
        {
            if (playback.BufferedDuration.TotalMilliseconds > 100) playback.ClearBuffer();
            playback.AddSamples(pcm, 0, pcm.Length);
        }

        public void Clear() => playback.ClearBuffer();

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { output?.Stop(); }
            finally { output?.Dispose(); }
        }
    }
}

public interface IVoiceDevice : IDisposable
{
    void Play(byte[] pcm);
    void Clear();
}
