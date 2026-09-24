using Common.Messaging;
using System;
using TaleWorlds.InputSystem;

namespace GameInterface.Services.UI.CoopOptions.Providers.UITab;

// Stores the local campaign player-list shortcut.
public sealed class PlayerListSectionOptions
{
    public InputKey ToggleKey { get; set; } = InputKey.O;

    // Keeps Escape reserved for closing menus and excludes non-keyboard inputs.
    public static bool IsSupported(InputKey key) => Enum.IsDefined(typeof(InputKey), key) &&
        key != InputKey.Escape && new Key(key).IsKeyboardInput;
}

// Updates the active overlay only after the options have been saved.
public sealed class PlayerListKeySelected : IEvent
{
    public InputKey Key { get; }

    // Carries the newly saved shortcut to the session service.
    public PlayerListKeySelected(InputKey key) => Key = key;
}
