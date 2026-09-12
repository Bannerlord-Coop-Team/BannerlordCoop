using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.UI.Donate;

/// <summary>
/// Shows the donation popup as a focus layer over the given screen. Mirrors
/// <see cref="CoopOptions.CoopOptionsOverlay"/>: add a layer, load the movie, remove it on close.
/// </summary>
internal sealed class DonatePopupOverlay
{
    private readonly ScreenBase owner;
    private DonatePopupVM dataSource;
    private GauntletLayer gauntletLayer;

    private DonatePopupOverlay(ScreenBase owner)
    {
        this.owner = owner;
    }

    public static void Show(ScreenBase owner)
    {
        if (owner == null) return;

        new DonatePopupOverlay(owner).Show();
    }

    private void Show()
    {
        dataSource = new DonatePopupVM(Close);
        gauntletLayer = new GauntletLayer("DonatePopupUI", 200)
        {
            IsFocusLayer = true
        };
        owner.AddLayer(gauntletLayer);
        gauntletLayer.InputRestrictions.SetInputRestrictions();
        gauntletLayer.LoadMovie("DonatePopupUIMovie", dataSource);
        ScreenManager.TrySetFocus(gauntletLayer);
    }

    private void Close()
    {
        if (gauntletLayer == null) return;

        gauntletLayer.IsFocusLayer = false;
        ScreenManager.TryLoseFocus(gauntletLayer);
        owner.RemoveLayer(gauntletLayer);
        dataSource = null;
        gauntletLayer = null;
    }
}
