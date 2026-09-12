using System;

namespace Common.Voice;

public interface IVoicePolicy
{
    float Gain(VoicePosition speaker, VoicePosition listener, VoiceRanges ranges);
    bool CanTransmit(VoiceActivation activation, bool canSpeak, bool focused, bool typing,
        bool muted, bool deafened, bool pushToTalk, float level, float threshold);
}

public sealed class VoicePolicy : IVoicePolicy
{
    public float Gain(VoicePosition speaker, VoicePosition listener, VoiceRanges ranges)
    {
        if (speaker == null || listener == null || ranges == null || !speaker.CanSpeak ||
            !speaker.CanHear || !listener.CanHear || speaker.Context != listener.Context ||
            string.IsNullOrEmpty(speaker.Context) || !speaker.IsFinite || !listener.IsFinite)
            return 0;

        double x = (double)speaker.X - listener.X;
        double y = (double)speaker.Y - listener.Y;
        double z = (double)speaker.Z - listener.Z;
        double distance = Math.Sqrt((x * x) + (y * y) + (z * z));
        float full = speaker.Context == VoicePosition.CampaignContext ? ranges.MapFull : ranges.SceneFull;
        float maximum = speaker.Context == VoicePosition.CampaignContext ? ranges.MapMaximum : ranges.SceneMaximum;
        if (distance >= maximum) return 0;
        if (distance <= full) return 1;
        return (float)((maximum - distance) / (maximum - full));
    }

    public bool CanTransmit(VoiceActivation activation, bool canSpeak, bool focused, bool typing,
        bool muted, bool deafened, bool pushToTalk, float level, float threshold)
    {
        if (!canSpeak || !focused || typing || muted || deafened) return false;
        return activation == VoiceActivation.PushToTalk ? pushToTalk :
            activation == VoiceActivation.VoiceActivity && level >= threshold;
    }
}

public enum VoiceActivation
{
    PushToTalk,
    VoiceActivity,
    Disabled
}
