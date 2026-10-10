using System;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade.GauntletUI;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.UI.CoopOptions;

public interface ICoopKeybindingPopupFactory
{
    ICoopKeybindingPopup Create(Action<Key> onDone, ScreenBase owner);
}

public sealed class CoopKeybindingPopupFactory : ICoopKeybindingPopupFactory
{
    public ICoopKeybindingPopup Create(Action<Key> onDone, ScreenBase owner) => new CoopKeybindingPopup(onDone, owner);
}

public interface ICoopKeybindingPopup
{
    bool IsActive { get; }
    void Toggle(bool active);
    void Tick();
}

internal sealed class CoopKeybindingPopup : ICoopKeybindingPopup
{
    private readonly KeybindingPopup popup;
    public CoopKeybindingPopup(Action<Key> onDone, ScreenBase owner) => popup = new KeybindingPopup(onDone, owner);
    public bool IsActive => popup.IsActive;
    public void Toggle(bool active) => popup.OnToggle(active);
    public void Tick() => popup.Tick();
}
