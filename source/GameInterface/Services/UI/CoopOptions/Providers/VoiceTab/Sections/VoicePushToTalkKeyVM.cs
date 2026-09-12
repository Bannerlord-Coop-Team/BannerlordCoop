using GameInterface.Services.Voice;
using System;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ViewModelCollection.GameOptions;
using TaleWorlds.MountAndBlade.ViewModelCollection.GameOptions.GameKeys;

namespace GameInterface.Services.UI.CoopOptions.Providers.VoiceTab.Sections;

// An unregistered GameKey keeps vanilla's editing VM without changing global controls.
public sealed class VoicePushToTalkKeyVM : GameKeyOptionVM
{
    public VoicePushToTalkKeyVM(InputKey key, Action<KeyOptionVM> request)
        : base(new GameKey(0, "PushToTalk", "CoopVoice", key), request,
            (option, value) =>
            {
                if (!new VoiceSettings().IsSupportedPushToTalkKey(value)) return;
                option.CurrentGameKey.KeyboardKey.ChangeKey(value);
                option.Update();
                ((VoicePushToTalkKeyVM)option).KeyChanged?.Invoke(value);
            }, null)
    {
        Update();
        Apply();
    }

    public event Action<InputKey> KeyChanged;

    public override void RefreshValues()
    {
        Name = "Push to talk";
        Description = "Keyboard / mouse binding for proximity voice";
        OptionValueText = Module.CurrentModule?.GlobalTextManager
            .GetHotKeyGameTextFromKeyID(CurrentKey.ToString().ToLowerInvariant()).ToString() ?? CurrentKey.InputKey.ToString();
        ExtraInformationText = "Controller push-to-talk remains D-pad right.";
    }

    public override void Update()
    {
        Key = CurrentGameKey.KeyboardKey;
        CurrentKey = new Key(Key.InputKey);
        RefreshValues();
    }
}
