using TaleWorlds.Engine.GauntletUI;
using System;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.UI.CoopOptions;

internal sealed class CoopOptionsOverlay
{
    private readonly ScreenBase owner;
    private CoopOptionsVM dataSource;
    private CoopOptionsGauntletLayer gauntletLayer;

    private CoopOptionsOverlay(ScreenBase owner)
    {
        this.owner = owner;
    }

    public static void Show(ScreenBase owner)
    {
        var overlay = new CoopOptionsOverlay(owner);
        overlay.Show();
    }

    private void Show()
    {
        if (!ContainerProvider.TryResolve<ICoopOptionsVMFactory>(out var factory))
            throw new InvalidOperationException("Coop options view-model factory is unavailable.");
        dataSource = factory.Create(Close);
        gauntletLayer = new CoopOptionsGauntletLayer(owner, dataSource)
        {
            IsFocusLayer = true
        };
        owner.AddLayer(gauntletLayer);
        gauntletLayer.InputRestrictions.SetInputRestrictions();
        gauntletLayer.LoadMovie("CoopOptionsUIMovie", dataSource);
        ScreenManager.TrySetFocus(gauntletLayer);
    }

    private void Close()
    {
        gauntletLayer.CloseKeybinding();
        gauntletLayer.IsFocusLayer = false;
        ScreenManager.TryLoseFocus(gauntletLayer);
        owner.RemoveLayer(gauntletLayer);
        dataSource?.OnFinalize();
        dataSource = null;
        gauntletLayer = null;
    }
}
