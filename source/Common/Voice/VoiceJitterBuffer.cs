using System;
using System.Collections.Generic;

namespace Common.Voice;

public interface IVoiceJitterBuffer
{
    bool Add(uint sequence, byte[] audio, long now);
    bool TryRead(long now, out byte[] audio);
    void Clear();
}

/// <summary>One speaker, one epoch. Called only by the audio worker; milliseconds use a monotonic clock.</summary>
public sealed class VoiceJitterBuffer : IVoiceJitterBuffer
{
    public const int FrameMilliseconds = 20;
    public const int TargetMilliseconds = 60;
    public const int MaximumAgeMilliseconds = 200;
    private readonly Dictionary<uint, (byte[] audio, long arrived)> frames = new();
    private uint next;
    private uint newest;
    private long nextDue;
    private long lastArrival;
    private bool started;
    private bool played;
    private int missing;

    public bool Add(uint sequence, byte[] audio, long now)
    {
        if (audio == null || audio.Length == 0 || audio.Length > VoicePacket.MaximumPayload) return false;
        if (started && now - lastArrival > MaximumAgeMilliseconds) Clear();
        if (started && unchecked((int)(sequence - newest)) <= -10) return false;
        if (started && played && unchecked((int)(sequence - next)) < 0) return false;
        if (frames.ContainsKey(sequence)) return false;
        if (!started)
        {
            started = true;
            next = newest = sequence;
            nextDue = now + TargetMilliseconds;
        }
        else if (!played && unchecked((int)(sequence - next)) < 0)
        {
            next = sequence;
        }
        if (unchecked((int)(sequence - newest)) > 0) newest = sequence;
        if (frames.Count >= 10) return false;
        frames.Add(sequence, (audio, now));
        lastArrival = now;
        return true;
    }

    public bool TryRead(long now, out byte[] audio)
    {
        audio = null;
        if (!started || now < nextDue) return false;
        if (now - nextDue > MaximumAgeMilliseconds || now - lastArrival > MaximumAgeMilliseconds)
        {
            Clear();
            return false;
        }
        played = true;
        nextDue += FrameMilliseconds;
        bool present = frames.TryGetValue(next, out var frame);
        frames.Remove(next++);
        if (present && now - frame.arrived <= MaximumAgeMilliseconds)
        {
            audio = frame.audio;
            missing = 0;
            return true;
        }
        // At most three concealed frames; never synthesize an indefinitely missing talkspurt.
        if (++missing <= 3) return true;
        Clear();
        return false;
    }

    public void Clear()
    {
        frames.Clear();
        started = played = false;
        missing = 0;
    }
}
