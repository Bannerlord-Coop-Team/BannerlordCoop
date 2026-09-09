using Common.Logging;
using Common.Voice;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading;

namespace GameInterface.Services.Voice;

public interface IVoiceAudio : IDisposable
{
    event Action<VoiceInputSnapshot, byte[]> Encoded;
    string Status { get; }
    bool IsTestingMicrophone { get; }
    float InputLevel { get; }
    IReadOnlyList<string> AudibleSpeakers { get; }
    IReadOnlyList<string> Microphones();
    void Update(VoiceInputSnapshot snapshot, VoiceSettings settings);
    void Receive(VoicePacket packet);
    void Retry();
    void TestMicrophone(bool enabled);
}

/// <summary>One bounded worker owns all codec/device state; no game objects cross this boundary.</summary>
public sealed class VoiceAudio : IVoiceAudio
{
    private sealed class Speaker
    {
        public long Epoch;
        public long LastArrival;
        public float Gain;
        public IVoiceDecoder Decoder;
        public IVoiceJitterBuffer Buffer;
    }

    private static readonly ILogger Logger = LogManager.GetLogger<VoiceAudio>();
    private readonly object gate = new();
    private readonly IVoiceCodecFactory codecs;
    private readonly IVoiceDeviceFactory devices;
    private readonly IVoiceCaptureFactory microphones;
    private readonly IVoicePolicy policy;
    private readonly IVoiceClock clock;
    private readonly Func<IVoiceJitterBuffer> buffers;
    private readonly Queue<(byte[] pcm, long epoch, long captured, bool test, float level)> capture = new();
    private readonly Queue<(VoicePacket packet, long arrived)> received = new();
    private readonly Dictionary<string, Speaker> speakers = new();
    private readonly Dictionary<string, long> speakerEpochs = new();
    private readonly Dictionary<string, long> speakerGenerations = new();
    private readonly Dictionary<string, long> talking = new();
    private readonly List<string> mixedSpeakers = new(10);
    private readonly Queue<string> epochOrder = new();
    private Thread worker;
    private VoiceInputSnapshot snapshot;
    private VoiceSettings settings = new();
    private bool retry = true;
    private bool clear;
    private bool disposed;
    private Exception failure;
    private Exception captureFailure;
    private string status = "Not started";
    private long testUntil;
    private float inputLevel;
    private long inputLevelAt;
    private long deviceGeneration;
    private long captureGeneration;

    public event Action<VoiceInputSnapshot, byte[]> Encoded;
    public bool IsTestingMicrophone { get { lock (gate) return !disposed && clock.Milliseconds < testUntil; } }
    public float InputLevel
    {
        get
        {
            lock (gate)
            {
                long now = clock.Milliseconds;
                if (disposed || snapshot == null || settings.GetActivation() == VoiceActivation.Disabled ||
                    !snapshot.Focused || snapshot.Typing || now - snapshot.SampledAt > 100 ||
                    (now >= testUntil && !policy.CanTransmit(settings.GetActivation(), snapshot.Position.CanSpeak,
                        snapshot.Focused, snapshot.Typing, settings.Muted, settings.Deafened, snapshot.PushToTalk, 1, settings.Sensitivity))) return 0;
                // Decay between callbacks so stopped capture cannot leave a lit meter.
                return inputLevel * Math.Max(0, Math.Min(1, 1 - ((now - inputLevelAt) / 100f)));
            }
        }
    }
    public IReadOnlyList<string> AudibleSpeakers
    {
        get
        {
            lock (gate)
            {
                long now = clock.Milliseconds;
                if (disposed || failure != null || clear || snapshot == null || !snapshot.Position.CanHear ||
                    settings.Deafened || !settings.Enabled || settings.Volume <= 0 || now < testUntil ||
                    now - snapshot.SampledAt > 100) return Array.Empty<string>();
                var active = new List<string>(10);
                foreach (var pair in talking)
                    if (now - pair.Value <= 200) active.Add(pair.Key);
                return active;
            }
        }
    }
    public string Status { get { lock (gate) return testUntil > clock.Milliseconds ? "Testing microphone locally (5 seconds)" : status; } }

    public VoiceAudio(IVoiceCodecFactory codecs, IVoiceDeviceFactory devices, IVoiceCaptureFactory microphones,
        IVoicePolicy policy, IVoiceClock clock, Func<IVoiceJitterBuffer> buffers)
    {
        this.codecs = codecs;
        this.devices = devices;
        this.microphones = microphones;
        this.policy = policy;
        this.clock = clock;
        this.buffers = buffers;
    }

    public IReadOnlyList<string> Microphones()
    {
        try { return microphones.Microphones(); }
        catch (Exception ex)
        {
            lock (gate) status = "Microphone unavailable: " + ex.Message + " Select Retry.";
            return new[] { "System default" };
        }
    }

    public void Update(VoiceInputSnapshot value, VoiceSettings options)
    {
        lock (gate)
        {
            if (disposed) return;
            if (snapshot?.Position.Epoch != value.Position.Epoch)
            {
                clear = retry = true;
                talking.Clear();
                inputLevel = 0;
                captureGeneration++;
                capture.Clear();
                received.Clear();
            }
            if (settings.Microphone != options.Microphone || settings.MicrophoneName != options.MicrophoneName ||
                settings.GetActivation() != options.GetActivation())
            {
                retry = true;
                inputLevel = 0;
                captureGeneration++;
                capture.Clear();
            }
            snapshot = value;
            settings = options;
            if (worker == null)
            {
                worker = new Thread(Run) { IsBackground = true, Name = "Coop voice" };
                worker.Start();
            }
        }
    }

    public void Receive(VoicePacket packet)
    {
        lock (gate)
        {
            if (disposed || snapshot == null || !packet.IsValid || packet.Audio.Length == 0 ||
                !snapshot.Position.CanHear || packet.ListenerEpoch != snapshot.Position.Epoch ||
                packet.Position.Context != snapshot.Position.Context || string.IsNullOrEmpty(packet.Speaker)) return;
            if (received.Count >= 100) received.Dequeue();
            received.Enqueue((packet, clock.Milliseconds));
        }
    }

    public void Retry() { lock (gate) { if (!disposed) retry = true; } }
    public void TestMicrophone(bool enabled)
    {
        lock (gate)
        {
            if (disposed) return;
            testUntil = enabled && settings.GetActivation() != VoiceActivation.Disabled ? clock.Milliseconds + 5000 : 0;
            inputLevel = 0;
            clear = retry = true;
            talking.Clear();
            captureGeneration++;
            capture.Clear();
        }
    }

    private void Captured(long generation, byte[] data, int count)
    {
        lock (gate)
        {
            if (disposed || generation != captureGeneration || snapshot == null || count != 1920) return;
            long now = clock.Milliseconds;
            bool testing = testUntil > now;
            if (now - snapshot.SampledAt > 100 || !snapshot.Focused || snapshot.Typing) return;
            if (!testing && !policy.CanTransmit(settings.GetActivation(), snapshot.Position.CanSpeak,
                snapshot.Focused, snapshot.Typing, settings.Muted, settings.Deafened,
                snapshot.PushToTalk, 1, settings.Sensitivity)) return;
            double sum = 0;
            for (int i = 0; i < count; i += 2)
            {
                short sample = BitConverter.ToInt16(data, i);
                sum += (double)sample * sample;
            }
            float level = Math.Min(1, (float)(Math.Sqrt(sum / (count / 2)) / 32768));
            inputLevel = level;
            inputLevelAt = now;
            if (capture.Count >= 3) capture.Dequeue();
            var copy = new byte[count];
            Buffer.BlockCopy(data, 0, copy, 0, count);
            capture.Enqueue((copy, snapshot.Position.Epoch, now, testing, level));
        }
    }

    private void SetFailure(Exception ex, long? generation = null)
    {
        lock (gate)
        {
            if (disposed || (generation.HasValue && generation != deviceGeneration)) return;
            failure = ex;
            talking.Clear();
            testUntil = 0;
            inputLevel = 0;
            status = "Voice unavailable: " + ex.Message + " Select Retry.";
        }
        Logger.Warning(ex, "Voice audio failed; gameplay continues");
    }

    private void CaptureFailed(long generation, Exception ex)
    {
        lock (gate)
        {
            if (disposed || generation != captureGeneration) return;
            captureFailure = ex;
            testUntil = 0;
            inputLevel = 0;
            captureGeneration++;
            capture.Clear();
            status = "Microphone unavailable: " + ex.Message + " Select Retry. Receiving voice remains available.";
        }
        Logger.Warning(ex, "Voice capture failed; playback continues");
    }

    private void Run()
    {
        IVoiceDevice device = null;
        IDisposable microphone = null;
        IVoiceEncoder encoder = null;
        long nextMix = clock.Milliseconds;
        try
        {
            while (true)
            {
                VoiceInputSnapshot current;
                VoiceSettings options;
                bool reopen;
                bool reset;
                bool failed;
                bool microphoneFailed;
                bool testing;
                long generation;
                long outputGeneration;
                lock (gate)
                {
                    if (disposed) break;
                    current = snapshot;
                    options = settings;
                    reopen = retry;
                    retry = false;
                    reset = clear;
                    clear = false;
                    failed = failure != null;
                    microphoneFailed = captureFailure != null;
                    failure = captureFailure = null;
                    if (reopen || failed || microphoneFailed)
                    {
                        captureGeneration++;
                        capture.Clear();
                    }
                    if (reset || failed || (reopen && (device == null || options.GetActivation() == VoiceActivation.Disabled))) deviceGeneration++;
                    generation = captureGeneration;
                    outputGeneration = deviceGeneration;
                    testing = testUntil > clock.Milliseconds && current.Focused && !current.Typing;
                }
                try
                {
                    bool restartCapture = reopen;
                    if (reopen || failed || microphoneFailed)
                    {
                        try { microphone?.Dispose(); }
                        catch (Exception ex)
                        {
                            restartCapture = false;
                            CaptureFailed(generation, ex);
                        }
                        finally { microphone = null; }
                    }
                    if (reset || failed || (reopen && options.GetActivation() == VoiceActivation.Disabled))
                    {
                        // Provider clearing cannot retract samples already submitted to the output device.
                        device?.Dispose();
                        device = null;
                        speakers.Clear();
                        lock (gate) received.Clear();
                        if (reopen && options.GetActivation() == VoiceActivation.Disabled) lock (gate) status = "Disabled";
                    }
                    if (reopen && options.GetActivation() != VoiceActivation.Disabled)
                    {
                        if (device == null)
                        {
                            device = devices.Open(ex => SetFailure(ex, outputGeneration));
                            nextMix = clock.Milliseconds;
                        }
                        encoder = codecs.CreateEncoder();
                        if (restartCapture)
                        {
                            lock (gate) { if (failure == null && captureFailure == null) status = "Ready"; }
                            try
                            {
                                microphone = microphones.Open(options,
                                    (data, count) => Captured(generation, data, count),
                                    ex => CaptureFailed(generation, ex));
                            }
                            catch (Exception ex) { CaptureFailed(generation, ex); }
                        }
                    }
                    if (reset)
                    {
                        speakers.Clear();
                        device?.Clear();
                        encoder = codecs.CreateEncoder();
                        nextMix = clock.Milliseconds;
                    }
                    if (device != null)
                    {
                        EncodeCaptured(current, options, encoder, device, testing);
                        long now = clock.Milliseconds;
                        // Drop a stalled timeline instead of replaying a burst of obsolete speech.
                        if (now - nextMix > 100)
                        {
                            speakers.Clear();
                            lock (gate) talking.Clear();
                            device.Clear();
                            nextMix = now;
                        }
                        ReceiveQueued(current, testing);
                        int mixed = 0;
                        while (now >= nextMix && mixed++ < 3)
                        {
                            Mix(current, options, device, now, testing);
                            nextMix += VoiceJitterBuffer.FrameMilliseconds;
                        }
                        if (now >= nextMix)
                        {
                            speakers.Clear();
                            lock (gate) talking.Clear();
                            device.Clear();
                            nextMix = now + VoiceJitterBuffer.FrameMilliseconds;
                        }
                    }
                }
                catch (Exception ex) { SetFailure(ex); }
                Thread.Sleep(5);
            }
        }
        catch (Exception ex) { SetFailure(ex); }
        finally
        {
            try { microphone?.Dispose(); }
            catch (Exception ex) { Logger.Warning(ex, "Voice microphone shutdown failed"); }
            try { device?.Dispose(); }
            catch (Exception ex) { Logger.Warning(ex, "Voice device shutdown failed"); }
        }
    }

    private void EncodeCaptured(VoiceInputSnapshot current, VoiceSettings options, IVoiceEncoder encoder,
        IVoiceDevice device, bool testing)
    {
        while (true)
        {
            (byte[] pcm, long epoch, long captured, bool test, float level) frame;
            lock (gate)
            {
                if (capture.Count == 0) return;
                frame = capture.Dequeue();
            }
            long now = clock.Milliseconds;
            if (frame.epoch != current.Position.Epoch || frame.test != testing ||
                now - frame.captured > 100 || now - current.SampledAt > 100) continue;
            var samples = new short[960];
            Buffer.BlockCopy(frame.pcm, 0, samples, 0, frame.pcm.Length);
            if (testing) { device.Play(frame.pcm); continue; }
            if (policy.CanTransmit(options.GetActivation(), current.Position.CanSpeak, current.Focused, current.Typing,
                options.Muted, options.Deafened, current.PushToTalk, frame.level, options.Sensitivity))
                Encoded?.Invoke(current, encoder.Encode(samples));
        }
    }

    private void ReceiveQueued(VoiceInputSnapshot current, bool testing)
    {
        while (true)
        {
            VoicePacket packet;
            long arrived;
            lock (gate)
            {
                if (received.Count == 0) return;
                (packet, arrived) = received.Dequeue();
            }
            if (clock.Milliseconds - arrived > 100 || testing || !current.Position.CanHear || packet.ListenerEpoch != current.Position.Epoch ||
                packet.Position.Context != current.Position.Context) continue;
            if (speakerGenerations.TryGetValue(packet.Speaker, out long highestGeneration))
            {
                if (packet.StreamGeneration < highestGeneration) continue;
                if (packet.StreamGeneration > highestGeneration)
                {
                    speakerEpochs.Remove(packet.Speaker);
                    speakers.Remove(packet.Speaker);
                    lock (gate) talking.Remove(packet.Speaker);
                }
            }
            if (speakerEpochs.TryGetValue(packet.Speaker, out long highestEpoch) && packet.Position.Epoch < highestEpoch) continue;
            if (!speakers.TryGetValue(packet.Speaker, out var speaker) || speaker.Epoch != packet.Position.Epoch)
            {
                if (speaker == null && speakers.Count >= 10) continue;
                speaker = new Speaker { Epoch = packet.Position.Epoch, Buffer = buffers(), Decoder = codecs.CreateDecoder() };
                lock (gate) talking.Remove(packet.Speaker);
                speakers[packet.Speaker] = speaker;
                RememberEpoch(packet.Speaker, packet.Position.Epoch, packet.StreamGeneration);
            }
            if (speaker.Buffer.Add(packet.Sequence, packet.Audio, arrived))
            {
                speaker.LastArrival = arrived;
                speaker.Gain = float.IsNaN(packet.Gain) ? 0 : Math.Max(0, Math.Min(1, packet.Gain));
            }
        }
    }

    private void RememberEpoch(string connection, long epoch, long generation)
    {
        if (!speakerGenerations.ContainsKey(connection))
        {
            // Retain retired connection epochs across decoder eviction, bounded over reconnects.
            if (speakerGenerations.Count >= 100)
            {
                string retired;
                do
                {
                    retired = epochOrder.Dequeue();
                    if (speakers.ContainsKey(retired)) epochOrder.Enqueue(retired);
                } while (speakers.ContainsKey(retired));
                speakerEpochs.Remove(retired);
                speakerGenerations.Remove(retired);
            }
            epochOrder.Enqueue(connection);
        }
        speakerEpochs[connection] = epoch;
        speakerGenerations[connection] = generation;
    }

    private void Mix(VoiceInputSnapshot current, VoiceSettings options, IVoiceDevice device, long now, bool testing)
    {
        var expired = new List<string>();
        var mix = new float[960];
        bool hasAudio = false;
        mixedSpeakers.Clear();
        foreach (var pair in speakers)
        {
            if (now - pair.Value.LastArrival > VoiceJitterBuffer.MaximumAgeMilliseconds)
            {
                expired.Add(pair.Key);
                continue;
            }
            if (!current.Position.CanHear || options.Deafened || testing || now - current.SampledAt > 100) continue;
            if (!pair.Value.Buffer.TryRead(now, out var audio)) continue;
            short[] samples;
            try { samples = pair.Value.Decoder.Decode(audio); }
            catch (Exception ex)
            {
                expired.Add(pair.Key);
                Logger.Warning(ex, "Voice decoding failed for {Speaker}; discarding stream", pair.Key);
                continue;
            }
            double energy = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float sample = samples[i] * pair.Value.Gain * options.Volume;
                mix[i] += sample;
                energy += (double)sample * sample;
            }
            // Measure only decoded, gain-adjusted speech submitted to the output device.
            if (audio != null && samples.Length > 0 && Math.Sqrt(energy / samples.Length) / 32768 >= 0.003)
                mixedSpeakers.Add(pair.Key);
            hasAudio = true;
        }
        foreach (string key in expired)
        {
            speakers.Remove(key);
            lock (gate) talking.Remove(key);
        }
        if (!hasAudio) return;
        var pcm = new short[960];
        for (int i = 0; i < pcm.Length; i++) pcm[i] = (short)(32767 * Math.Tanh(mix[i] / 32767));
        var bytes = new byte[1920];
        Buffer.BlockCopy(pcm, 0, bytes, 0, bytes.Length);
        device.Play(bytes);
        lock (gate)
        {
            if (disposed || clear || snapshot?.Position.Epoch != current.Position.Epoch) return;
            foreach (string speaker in mixedSpeakers) talking[speaker] = now;
        }
    }

    public void Dispose()
    {
        Thread stopping;
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            talking.Clear();
            capture.Clear();
            received.Clear();
            stopping = worker;
            captureGeneration++;
            deviceGeneration++;
        }
        if (stopping != Thread.CurrentThread) stopping?.Join();
    }
}
