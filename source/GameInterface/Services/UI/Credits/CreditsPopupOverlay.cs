using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.UI.Credits;

/// <summary>
/// Shows the credits popup as a focus layer over the given screen. Mirrors
/// <see cref="Donate.DonatePopupOverlay"/>: add a layer, load the movie, remove it on close.
/// </summary>
internal sealed class CreditsPopupOverlay
{
    private readonly ScreenBase owner;
    private CreditsPopupVM dataSource;
    private GauntletLayer gauntletLayer;

    private CreditsPopupOverlay(ScreenBase owner)
    {
        this.owner = owner;
    }

    public static void Show(ScreenBase owner)
    {
        if (owner == null) return;

        new CreditsPopupOverlay(owner).Show();
    }

    private void Show()
    {
        dataSource = new CreditsPopupVM(Close);
        gauntletLayer = new GauntletLayer("CreditsPopupUI", 200)
        {
            IsFocusLayer = true
        };
        owner.AddLayer(gauntletLayer);
        gauntletLayer.InputRestrictions.SetInputRestrictions();
        gauntletLayer.LoadMovie("CreditsPopupUIMovie", dataSource);
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
