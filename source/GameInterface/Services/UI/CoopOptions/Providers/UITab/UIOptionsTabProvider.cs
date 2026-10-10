using Common.Messaging;
using GameInterface.Configuration;
using System;
using TaleWorlds.InputSystem;

namespace GameInterface.Services.UI.CoopOptions.Providers.UITab;

/// <summary>Groups client display preferences into one options tab.</summary>
public sealed class UIOptionsTabProvider : ICoopOptionsTabProvider
{
    public const string TabId = "UITab";
    public string Id => TabId;

    // Reads the persisted shortcut, retaining O for missing or unsupported values.
    public static InputKey GetPlayerListKey(CoopOptionsData options)
    {
        var key = options.GetSectionOrDefault(TabId, "PlayerList", new PlayerListSectionOptions()).ToggleKey;
        return PlayerListSectionOptions.IsSupported(key) ? key : InputKey.O;
    }

    // Display preferences are available on every server.
    public bool IsAvailable(ModOptions modOptions) => true;

    // Creates the grouped editor without changing existing preference storage keys.
    public CoopOptionsTabVM CreateTab(CoopOptionsData options, IMessageBroker messageBroker,
        Action<CoopOptionsTabVM> onSelect)
    {
        return new CoopOptionsTabVM(Id, "UI",
            new[] { new UISection(options, messageBroker) }, onSelect);
    }
}
