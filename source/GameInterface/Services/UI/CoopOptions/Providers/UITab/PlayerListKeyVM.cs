using System;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade.ViewModelCollection.GameOptions;
using TaleWorlds.MountAndBlade.ViewModelCollection.GameOptions.GameKeys;

namespace GameInterface.Services.UI.CoopOptions.Providers.UITab;

// Edits a local player-list key without modifying vanilla controls.
public sealed class PlayerListKeyVM : GameKeyOptionVM
{
    // Uses the shared capture popup and keeps edits pending until Apply.
    public PlayerListKeyVM(InputKey key, Action<KeyOptionVM> request)
        : base(new GameKey(0, "PlayerList", "CoopUI", key), request, SetBinding, null)
    {
        Update();
        Apply();
    }

    // Rejects cancel/controller input and updates the pending keyboard binding.
    private static void SetBinding(GameKeyOptionVM option, InputKey key)
    {
        if (!PlayerListSectionOptions.IsSupported(key)) return;
        option.CurrentGameKey.KeyboardKey.ChangeKey(key);
        option.Update();
    }

    // Labels the editor without relying on vanilla game-key localization ids.
    public override void RefreshValues()
    {
        Name = "Player list";
        Description = "Toggle the player list on the campaign map.";
        OptionValueText = CurrentKey.InputKey.ToString();
        ExtraInformationText = "Choose a key not used by another control. Default: O.";
    }

    // Displays the keyboard binding even when a controller is active.
    public override void Update()
    {
        Key = CurrentGameKey.KeyboardKey;
        CurrentKey = new Key(Key.InputKey);
        RefreshValues();
        UpdateIsChanged();
    }
}
