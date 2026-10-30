using System;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.InputSystem;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.UI.ServerInfo;

/// <summary>The panel's screen side, kept apart so the open rules run without Gauntlet.</summary>
internal interface IServerInfoPopup : IDisposable
{
    bool CanOpen();
    void Open();
    void Close();
}

/// <summary>Shows the server info panel over the campaign map without pausing the shared world.</summary>
internal sealed class ServerInfoOverlay : GlobalLayer, IServerInfoPopup
{
    // Only answered on the campaign map, through the same guard as the player list key.
    internal const InputKey ToggleKey = InputKey.F3;

    private readonly ServerInfoVM viewModel;
    private readonly IMapAvailability mapAvailability;
    private readonly Action update;
    private readonly Action toggle;
    private GauntletLayer layer;
    private GauntletMovieIdentifier movie;

    // Receives the panel state, the map check shared with the player list, the service's per-frame check and its F3 action.
    public ServerInfoOverlay(ServerInfoVM viewModel, IMapAvailability mapAvailability, Action update, Action toggle)
    {
        this.viewModel = viewModel;
        this.mapAvailability = mapAvailability;
        this.update = update;
        this.toggle = toggle;
    }

    // Sits just above the player list (115) and stays unfocused until the panel opens.
    public void Initialize()
    {
        layer = new GauntletLayer("CoopServerInfo", 116);
        movie = layer.LoadMovie("CoopServerInfoUIMovie", viewModel);
        layer.InputRestrictions.ResetInputRestrictions();
        Layer = layer;
        ScreenManager.AddGlobalLayer(this, false);
    }

    // Closes on the same conditions as the player list, routes F3 to the service, and while closed lets pending info open.
    protected override void OnTick(float dt)
    {
        base.OnTick(dt);
        if (viewModel.IsOpen && (!mapAvailability.IsMapAvailable() || !ReferenceEquals(ScreenManager.FocusedLayer, layer)))
        {
            Close();
            return;
        }
        if (Input.IsKeyPressed(ToggleKey)) toggle();
        else if (!viewModel.IsOpen) update();
        else if (Input.IsKeyReleased(InputKey.Escape)) viewModel.HandleEscape();
        else viewModel.Tick(dt);
    }

    public bool CanOpen() => mapAvailability.CanOpenOverlay();

    // Takes input focus like the open player list.
    public void Open()
    {
        if (viewModel.IsOpen) return;
        viewModel.IsOpen = true;
        layer.InputRestrictions.SetInputRestrictions();
        layer.IsFocusLayer = true;
        ScreenManager.TrySetFocus(layer);
    }

    // Releases focus without changing campaign time controls.
    public void Close()
    {
        if (!viewModel.IsOpen) return;
        viewModel.IsOpen = false;
        layer.IsFocusLayer = false;
        ScreenManager.TryLoseFocus(layer);
        layer.InputRestrictions.ResetInputRestrictions();
    }

    // Removes the session's global layer and releases its movie resources.
    public void Dispose()
    {
        Close();
        layer.ReleaseMovie(movie);
        ScreenManager.RemoveGlobalLayer(this);
        Layer = null;
        layer = null;
    }
}
