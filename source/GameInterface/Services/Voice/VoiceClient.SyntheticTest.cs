#if DEBUG
using Common.Voice;
using System;

namespace GameInterface.Services.Voice;

public interface IVoiceSyntheticTest
{
    string StartSyntheticVoiceTest(IVoiceCodecFactory codecs);
    string SyntheticVoiceTestStatus { get; }
    string StopSyntheticVoiceTest();
}

public sealed partial class VoiceClient : IVoiceSyntheticTest
{
    private IVoiceEncoder syntheticEncoder;
    private readonly short[] syntheticSamples = new short[960];
    private long syntheticUntil;
    private long syntheticNextFrame;
    private long syntheticEpoch;
    private string syntheticContext;
    private VoiceActivation syntheticMode;
    private int syntheticFrames;
    private int syntheticDiscardedFrames;
    private string syntheticStatus = "Synthetic voice idle";

    public string StartSyntheticVoiceTest(IVoiceCodecFactory codecs)
    {
        lock (gate)
        {
            if (syntheticEncoder != null)
            {
                if (clock.Milliseconds < syntheticUntil) return "Synthetic voice already running; deadline unchanged";
                FinishSyntheticVoiceTest("Five-second deadline reached");
            }
            syntheticMode = settings.Activation;
            if (!SyntheticVoiceEligible()) return "Synthetic voice rejected: current client is not eligible to speak";
            try
            {
                syntheticEncoder = codecs.CreateEncoder();
                if (syntheticEncoder == null) throw new InvalidOperationException("Encoder unavailable");
                syntheticFrames = 0;
                syntheticDiscardedFrames = 0;
                syntheticEpoch = snapshot.Position.Epoch;
                syntheticContext = snapshot.Position.Context;
                syntheticNextFrame = clock.Milliseconds;
                syntheticUntil = syntheticNextFrame + 5000;
                syntheticStatus = "Synthetic voice running (fixed tone, at most 5 seconds; delivery unconfirmed)";
                Logger.Information("{SyntheticVoiceStatus} in {Context}", syntheticStatus, syntheticContext);
            }
            catch (Exception ex)
            {
                syntheticStatus = "Synthetic voice stopped: encoder start failed: " + ex.Message;
                FinishSyntheticVoiceTest("Encoder start failed: " + ex.Message);
            }
            return syntheticStatus;
        }
    }

    public string SyntheticVoiceTestStatus
    {
        get
        {
            lock (gate)
            {
                if (syntheticEncoder != null && clock.Milliseconds >= syntheticUntil)
                    FinishSyntheticVoiceTest("Five-second deadline reached");
                return syntheticStatus + "; frames submitted=" + syntheticFrames + "; context=" + syntheticContext +
                    "; stale frames discarded=" + syntheticDiscardedFrames;
            }
        }
    }

    public string StopSyntheticVoiceTest()
    {
        lock (gate)
        {
            FinishSyntheticVoiceTest("Explicit stop");
            return syntheticStatus;
        }
    }

    private bool SyntheticVoiceEligible() => SyntheticVoiceIneligibility() == null;

    private string SyntheticVoiceIneligibility(bool requireFreshSnapshot = true)
    {
        if (disposed) return "Client disposed";
        if (error != null) return "Voice error: " + error;
        if (!serverEnabled) return "Server voice disabled";
        if (ranges == null) return "Server voice configuration missing";
        if (snapshot == null) return "Input snapshot missing";
        if (!snapshot.Position.CanSpeak) return "Position cannot speak";
        if (!snapshot.Position.CanHear) return "Position cannot hear";
        if (string.IsNullOrEmpty(snapshot.Position.Context)) return "Position context missing";
        if (snapshot.Typing) return "Typing active";
        if (!settings.Enabled) return "Local voice disabled";
        if (settings.Muted) return "Locally muted";
        if (settings.Deafened) return "Locally deafened";
        if (settings.Activation != syntheticMode) return "Activation mode changed";
        if (keybindCapture) return "Keybinding capture active";
        if (keybindCaptureEnding) return "Keybinding capture ending";
        if (clock.Milliseconds < testUntil) return "Local microphone test active";
        if (requireFreshSnapshot && clock.Milliseconds - snapshot.SampledAt > 100)
            return "Input snapshot stale; age ms=" + (clock.Milliseconds - snapshot.SampledAt);
        return null;
    }

    private string SyntheticVoiceStopReason(long now, bool requireFreshSnapshot)
    {
        if (now >= syntheticUntil) return "Five-second deadline reached";
        if (syntheticFrames + syntheticDiscardedFrames >= 250) return "250-frame encode limit reached";
        var reason = SyntheticVoiceIneligibility(requireFreshSnapshot);
        if (reason != null) return reason;
        if (snapshot.Position.Epoch != syntheticEpoch) return "Position epoch changed";
        if (snapshot.Position.Context != syntheticContext) return "Position context changed";
        return null;
    }

    private void TickSyntheticVoiceTest()
    {
        if (syntheticEncoder == null) return;
        long now = clock.Milliseconds;
        var reason = SyntheticVoiceStopReason(now, requireFreshSnapshot: true);
        if (reason != null)
        {
            FinishSyntheticVoiceTest(reason);
            return;
        }
        if (now < syntheticNextFrame) return;
        // One frame per tick, never replay a backlog after a stalled game frame.
        syntheticNextFrame = now + 20;
        try
        {
            for (int i = 0; i < syntheticSamples.Length; i++)
                syntheticSamples[i] = (short)(2000 * Math.Sin(2 * Math.PI * 440 * ((syntheticFrames * 960L) + i) / 48000));
            long encodeStarted = clock.Milliseconds;
            byte[] encoded = syntheticEncoder.Encode(syntheticSamples);
            now = clock.Milliseconds;
            reason = SyntheticVoiceStopReason(now, requireFreshSnapshot: false);
            if (reason != null)
            {
                FinishSyntheticVoiceTest(reason + " during encoding");
                return;
            }
            if (now - snapshot.SampledAt > 100)
            {
                // Discard stale payloads; the next tick must sample fresh input within the original deadline.
                syntheticDiscardedFrames++;
                Logger.Information("Synthetic voice discarded stale frame; snapshot age ms={Age}; encode ms={Encode}; discarded={Discarded}",
                    now - snapshot.SampledAt, now - encodeStarted, syntheticDiscardedFrames);
                return;
            }
            // This private test branch does not change snapshots or normal microphone callback gates.
            SendVoicePacket(encoded);
            syntheticFrames++;
        }
        catch (Exception ex)
        {
            FinishSyntheticVoiceTest("Synthetic encode/send failed: " + ex.Message);
        }
    }

    private void FinishSyntheticVoiceTest(string reason)
    {
        var encoder = syntheticEncoder;
        if (encoder == null) return;
        syntheticEncoder = null;
        syntheticUntil = 0;
        syntheticStatus = "Synthetic voice stopped: " + reason;
        try { (encoder as IDisposable)?.Dispose(); }
        catch (Exception ex) { Logger.Warning(ex, "Synthetic voice encoder disposal failed"); }
        Logger.Information("{SyntheticVoiceStatus}; frames submitted={Frames}; stale frames discarded={Discarded}",
            syntheticStatus, syntheticFrames, syntheticDiscardedFrames);
    }
}
#endif
