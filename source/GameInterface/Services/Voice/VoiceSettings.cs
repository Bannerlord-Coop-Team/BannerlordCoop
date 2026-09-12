using Common.Voice;
using System;
using TaleWorlds.InputSystem;

namespace GameInterface.Services.Voice;

public sealed class VoiceSettings
{
    public bool Enabled { get; set; } = true;
    public VoiceActivation Activation { get; set; } = VoiceActivation.PushToTalk;
    public InputKey PushToTalkKey { get; set; } = InputKey.Q;
    public int Microphone { get; set; } = -1;
    public string MicrophoneName { get; set; }
    public float Sensitivity { get; set; } = 0.02f;
    public float Volume { get; set; } = 1;
    public bool Muted { get; set; }
    public bool Deafened { get; set; }
    public bool ShowTalkingPlayers { get; set; } = true;

    public VoiceActivation GetActivation() => Enabled ? Activation : VoiceActivation.Disabled;

    public bool IsSupportedPushToTalkKey(InputKey key)
    {
        if (!Enum.IsDefined(typeof(InputKey), key) || key == InputKey.Escape) return false;
        var input = new Key(key);
        return input.IsKeyboardInput || input.IsMouseButtonInput;
    }

    public VoiceSettings Validated()
    {
        return new VoiceSettings
        {
            Enabled = Enabled && Activation != VoiceActivation.Disabled,
            Activation = Enum.IsDefined(typeof(VoiceActivation), Activation) && Activation != VoiceActivation.Disabled
                ? Activation : VoiceActivation.PushToTalk,
            PushToTalkKey = IsSupportedPushToTalkKey(PushToTalkKey) ? PushToTalkKey : InputKey.Q,
            Microphone = Math.Max(-1, Math.Min(255, Microphone)),
            MicrophoneName = MicrophoneName,
            Sensitivity = float.IsNaN(Sensitivity) ? 0.02f : Math.Max(0.001f, Math.Min(1, Sensitivity)),
            Volume = float.IsNaN(Volume) ? 1 : Math.Max(0, Math.Min(1, Volume)),
            Muted = Muted,
            Deafened = Deafened,
            ShowTalkingPlayers = ShowTalkingPlayers
        };
    }
}

public interface IVoiceSceneSource
{
    (string Context, bool IsSpectator) GetScene();
}

public sealed class VoiceInputSnapshot
{
    public VoicePosition Position { get; }
    public bool Focused { get; }
    public bool Typing { get; }
    public bool PushToTalk { get; }
    public long SampledAt { get; }

    public VoiceInputSnapshot(VoicePosition position, bool focused, bool typing, bool pushToTalk, long sampledAt)
    {
        Position = position;
        Focused = focused;
        Typing = typing;
        PushToTalk = pushToTalk;
        SampledAt = sampledAt;
    }
}
