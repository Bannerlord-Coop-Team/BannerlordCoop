using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.UI.CoopOptions;

internal sealed class CoopOptionsOverlay
{
    private readonly ScreenBase owner;
    private CoopOptionsVM dataSource;
    private GauntletLayer gauntletLayer;

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
        dataSource = new CoopOptionsVM(Close);
        gauntletLayer = new GauntletLayer("CoopOptionsUI", 100)
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
        gauntletLayer.IsFocusLayer = false;
        ScreenManager.TryLoseFocus(gauntletLayer);
        owner.RemoveLayer(gauntletLayer);
        dataSource = null;
        gauntletLayer = null;
    }
}
