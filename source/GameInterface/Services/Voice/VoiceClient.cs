using Common.Logging;
using Common.Network;
using Common.Voice;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.CoopOptions.Providers.VoiceTab;
using Serilog;
using System;
using System.Collections.Generic;

namespace GameInterface.Services.Voice;

public interface IVoiceClient : IDisposable
{
    event Action StatusChanged;
    void Tick();
    void Configure(VoiceRanges ranges, long sentAt);
    void SetServerEnabled(bool enabled);
    IReadOnlyList<string> AudibleSpeakers { get; }
    void Receive(VoicePacket packet);
    void Apply(VoiceSettings settings);
    VoiceSettings Settings { get; }
    string Status { get; }
    bool IsTestingMicrophone { get; }
    float InputLevel { get; }
    IReadOnlyList<string> Microphones();
    void Retry();
    void TestMicrophone(bool enabled);
    void SetKeybindCapture(bool active);
}

public sealed partial class VoiceClient : IVoiceClient
{
    private static readonly ILogger Logger = LogManager.GetLogger<VoiceClient>();
    private readonly object gate = new();
    private readonly IVoiceGameInput input;
    private readonly IVoiceAudio audio;
    private readonly IVoiceClock clock;
    private readonly INetwork network;
    private readonly IVoiceTransitWindow transit;
    private VoiceInputSnapshot snapshot;
    private VoiceSettings settings;
    private VoiceRanges ranges;
    private long epoch;
    private long lastHeartbeat;
    private long testUntil;
    private uint audioSequence;
    private uint stateSequence;
    private bool disposed;
    private bool serverEnabled = true;
    private bool keybindCapture;
    private bool keybindCaptureEnding;
    private bool contextChangedBySettings;
    private string error;
    private string lastStatus;
    private bool lastTestingMicrophone;
    private float lastInputLevel;
    public event Action StatusChanged;

    public VoiceClient(IVoiceGameInput input, IVoiceAudio audio, IVoiceClock clock,
        INetwork network, ICoopOptionsStore store, IVoiceTransitWindow transit)
    {
        this.input = input;
        this.audio = audio;
        this.clock = clock;
        this.network = network;
        this.transit = transit;
        settings = VoiceOptionsTabProvider.Load(store.LoadOrDefault()).Validated();
        audio.Encoded += SendAudio;
    }

    public IReadOnlyList<string> AudibleSpeakers
    {
        get { lock (gate) return !disposed && serverEnabled && settings.ShowTalkingPlayers && settings.Enabled && !settings.Deafened
            ? audio.AudibleSpeakers : Array.Empty<string>(); }
    }

    public void SetServerEnabled(bool enabled)
    {
        lock (gate)
        {
            if (disposed || enabled == serverEnabled) return;
            serverEnabled = enabled;
#if DEBUG
            FinishSyntheticVoiceTest("Server voice eligibility changed");
#endif
            epoch++;
            contextChangedBySettings = true;
            testUntil = 0;
            audio.TestMicrophone(false);
            if (snapshot != null)
            {
                var point = snapshot.Position;
                snapshot = new VoiceInputSnapshot(new VoicePosition(point.Context, epoch, point.X, point.Y, point.Z, false, false),
                    snapshot.Focused, snapshot.Typing, false, snapshot.SampledAt);
                audio.Update(snapshot, EffectiveSettings());
            }
        }
    }

    private VoiceSettings EffectiveSettings()
    {
        var effective = settings.Validated();
        effective.Enabled &= serverEnabled;
        return effective;
    }

    public VoiceSettings Settings { get { lock (gate) return settings.Validated(); } }
    public string Status { get { lock (gate) return !serverEnabled ? "Disabled by server" : !settings.Enabled ? "Disabled" : error ?? (ranges == null ? "Waiting for server voice configuration" : audio.Status); } }
    public bool IsTestingMicrophone { get { lock (gate) return !disposed && serverEnabled && error == null && settings.Enabled && clock.Milliseconds < testUntil && audio.IsTestingMicrophone; } }
    public float InputLevel
    {
        get
        {
            lock (gate)
            {
                if (disposed || !serverEnabled || error != null || !settings.Enabled || keybindCapture || keybindCaptureEnding ||
                    (!IsTestingMicrophone && (settings.Muted || settings.Deafened))) return 0;
                return audio.InputLevel;
            }
        }
    }
    public IReadOnlyList<string> Microphones() => audio.Microphones();
    public void Retry() { lock (gate) error = null; audio.Retry(); }
    public void TestMicrophone(bool enabled)
    {
        lock (gate)
        {
            if (disposed) return;
            enabled = enabled && serverEnabled && settings.Enabled;
#if DEBUG
            if (enabled) FinishSyntheticVoiceTest("Local microphone test started");
#endif
            try
            {
                testUntil = enabled ? clock.Milliseconds + 5000 : 0;
                audio.TestMicrophone(enabled);
            }
            catch (Exception ex)
            {
                testUntil = 0;
                error = "Microphone test unavailable: " + ex.Message + " Select Retry.";
                Logger.Warning(ex, "Voice microphone test failed; gameplay continues");
            }
        }
    }

    public void Configure(VoiceRanges value, long sentAt)
    {
        lock (gate)
        {
            if (!disposed && value != null && value.IsValid)
            {
                ranges = value;
                transit.Accept(sentAt, clock.Milliseconds);
            }
        }
    }

    public void Apply(VoiceSettings value)
    {
        lock (gate)
        {
            var updated = value.Validated();
            // Fence audio sampled with the previous binding until input is sampled again.
            if (updated.PushToTalkKey != settings.PushToTalkKey) keybindCaptureEnding = true;
            settings = updated;
#if DEBUG
            if (syntheticEncoder != null && !SyntheticVoiceEligible()) FinishSyntheticVoiceTest("Local voice eligibility changed");
#endif
            if (!settings.Enabled)
            {
                testUntil = 0;
                audio.TestMicrophone(false);
            }
            // Apply settings to the current audio snapshot; relaxing permissions still waits for fresh input.
            if (snapshot != null)
            {
                var point = snapshot.Position;
                bool hear = serverEnabled && settings.Enabled && !settings.Deafened && point.CanHear;
                bool speak = hear && !settings.Muted && point.CanSpeak;
                if (hear != point.CanHear || speak != point.CanSpeak)
                {
                    epoch++;
                    contextChangedBySettings = true;
                }
                point = new VoicePosition(point.Context, epoch, point.X, point.Y, point.Z, speak, hear);
                snapshot = new VoiceInputSnapshot(point, snapshot.Focused, snapshot.Typing, snapshot.PushToTalk, snapshot.SampledAt);
                audio.Update(snapshot, EffectiveSettings());
            }
        }
    }

    public void SetKeybindCapture(bool active)
    {
        lock (gate)
        {
            keybindCapture = active;
            keybindCaptureEnding = true;
#if DEBUG
            FinishSyntheticVoiceTest("Keybinding capture changed");
#endif
        }
    }

    public void Tick()
    {
        try
        {
            lock (gate)
            {
                if (disposed || error != null) return;
                var sampled = input.Sample(epoch, settings);
                var point = sampled.Position;
                bool enabled = serverEnabled && ranges != null && settings.GetActivation() != VoiceActivation.Disabled;
                bool hear = enabled && !settings.Deafened && point.CanHear;
                bool speak = hear && !settings.Muted && point.CanSpeak && !keybindCapture && !keybindCaptureEnding;
                bool changed = contextChangedBySettings || snapshot == null || snapshot.Position.Context != point.Context ||
                    snapshot.Position.CanHear != hear || snapshot.Position.CanSpeak != speak;
                if (changed && !contextChangedBySettings) epoch++;
                point = new VoicePosition(point.Context, epoch, point.X, point.Y, point.Z, speak, hear);
                snapshot = new VoiceInputSnapshot(point, sampled.Focused, sampled.Typing, sampled.PushToTalk, sampled.SampledAt);
                keybindCaptureEnding = false;
                if (ranges == null) return;
                audio.Update(snapshot, EffectiveSettings());
                if (changed) network.SendAll(new VoiceContextChanged { Position = point, SentAt = clock.Milliseconds });
                contextChangedBySettings = false;
                if (clock.Milliseconds - lastHeartbeat >= 100 || changed)
                {
                    network.SendAll(new VoicePacket
                    {
                        Position = point, Audio = Array.Empty<byte>(), StateSequence = ++stateSequence, SentAt = clock.Milliseconds
                    });
                    lastHeartbeat = clock.Milliseconds;
                }
#if DEBUG
                TickSyntheticVoiceTest();
#endif
            }
        }
        catch (Exception ex)
        {
            lock (gate)
            {
                error = "Voice unavailable: " + ex.Message + " Select Retry.";
#if DEBUG
                FinishSyntheticVoiceTest("Voice tick failed");
#endif
            }
            Logger.Warning(ex, "Voice tick failed; gameplay continues");
        }
        finally
        {
            string currentStatus = Status;
            bool testing = IsTestingMicrophone;
            float level = InputLevel;
            if (lastStatus != currentStatus || lastTestingMicrophone != testing || lastInputLevel != level)
            {
                lastStatus = currentStatus;
                lastTestingMicrophone = testing;
                lastInputLevel = level;
                StatusChanged?.Invoke();
            }
        }
    }

    private void SendAudio(VoiceInputSnapshot captured, byte[] data)
    {
        lock (gate)
        {
            if (disposed || !serverEnabled || ranges == null || snapshot == null || keybindCapture || keybindCaptureEnding || clock.Milliseconds < testUntil ||
                settings.Muted || settings.Deafened || settings.GetActivation() == VoiceActivation.Disabled ||
                captured.Position.Epoch != snapshot.Position.Epoch ||
                !snapshot.Position.CanSpeak || !snapshot.Focused || snapshot.Typing ||
                clock.Milliseconds - snapshot.SampledAt > 100 ||
                (settings.GetActivation() == VoiceActivation.PushToTalk && !snapshot.PushToTalk)) return;
            SendVoicePacket(data);
        }
    }

    private void SendVoicePacket(byte[] data)
    {
        network.SendAll(new VoicePacket
        {
            Position = snapshot.Position, Audio = data, Sequence = ++audioSequence, StateSequence = ++stateSequence, SentAt = clock.Milliseconds
        });
    }

    public void Receive(VoicePacket packet)
    {
        lock (gate)
        {
            if (!disposed && serverEnabled && settings.Enabled && packet != null && transit.Accept(packet.SentAt, clock.Milliseconds)) audio.Receive(packet);
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
#if DEBUG
            FinishSyntheticVoiceTest("Client disposed");
#endif
            audio.Encoded -= SendAudio;
        }
        audio.Dispose();
    }
}
