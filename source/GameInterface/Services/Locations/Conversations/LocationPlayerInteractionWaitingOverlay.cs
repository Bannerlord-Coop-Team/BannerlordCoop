using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.Locations.Conversations;

internal sealed class LocationPlayerInteractionWaitingOverlay : GlobalLayer
{
    private static LocationPlayerInteractionWaitingOverlay instance;

    private LocationPlayerInteractionWaitingOverlayVM dataSource;
    private GauntletLayer gauntletLayer;
    private bool isShown;

    public static LocationPlayerInteractionWaitingOverlay Instance => instance ??= new LocationPlayerInteractionWaitingOverlay();

    public void Show(string otherPlayerName)
    {
        if (isShown) return;

        dataSource = new LocationPlayerInteractionWaitingOverlayVM(otherPlayerName);
        gauntletLayer = new GauntletLayer("CoopLocationPlayerInteractionWaitingOverlay", 1000)
        {
            IsFocusLayer = true
        };
        gauntletLayer.InputRestrictions.SetInputRestrictions();
        gauntletLayer.LoadMovie("CoopLocationPlayerInteractionWaitingOverlay", dataSource);
        Layer = gauntletLayer;

        ScreenManager.AddGlobalLayer(this, false);
        ScreenManager.TrySetFocus(gauntletLayer);
        isShown = true;
    }

    public void Hide()
    {
        if (!isShown) return;

        gauntletLayer.IsFocusLayer = false;
        ScreenManager.TryLoseFocus(gauntletLayer);
        ScreenManager.RemoveGlobalLayer(this);
        gauntletLayer = null;
        dataSource = null;
        isShown = false;
    }
}

internal sealed class LocationPlayerInteractionWaitingOverlayVM : ViewModel
{
    public LocationPlayerInteractionWaitingOverlayVM(string otherPlayerName)
    {
        var name = string.IsNullOrEmpty(otherPlayerName) ? "Another player" : otherPlayerName;
        WaitingText = $"Awaiting proposal from {name}...";
    }

    [DataSourceProperty]
    public string WaitingText { get; }
}
