using GameInterface.Services.Chat;
using SandBox.View.Map;
using System;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.InputSystem;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.UI.ServerInfo;

/// <summary>The panel's screen side, kept apart so the show-once rules run without Gauntlet.</summary>
internal interface IServerInfoPopup : IDisposable
{
    bool CanOpen();
    void Open();
    void Close();
}

/// <summary>Shows the server info panel over the campaign map without pausing the shared world.</summary>
internal sealed class ServerInfoOverlay : GlobalLayer, IServerInfoPopup
{
    private readonly ServerInfoVM viewModel;
    private readonly IChatService chat;
    private readonly IMapAvailability mapAvailability;
    private readonly Action update;
    private GauntletLayer layer;
    private GauntletMovieIdentifier movie;

    // Receives the panel state, the chat focus guard, the map check shared with the player list and the service's per-frame check.
    public ServerInfoOverlay(ServerInfoVM viewModel, IChatService chat, IMapAvailability mapAvailability, Action update)
    {
        this.viewModel = viewModel;
        this.chat = chat;
        this.mapAvailability = mapAvailability;
        this.update = update;
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

    // Closes on the same conditions as the player list; while closed the service may open pending info.
    protected override void OnTick(float dt)
    {
        base.OnTick(dt);
        if (!viewModel.IsOpen)
        {
            update();
            return;
        }
        if (!mapAvailability.IsMapAvailable() || !ReferenceEquals(ScreenManager.FocusedLayer, layer)) Close();
        else if (Input.IsKeyReleased(InputKey.Escape)) viewModel.HandleEscape();
        else viewModel.Tick(dt);
    }

    // Uses the player list's toggle rules, so it waits while the list, chat typing or another modal has focus.
    public bool CanOpen()
    {
        if (!mapAvailability.IsMapAvailable() || chat.IsTyping || Input.IsOnScreenKeyboardActive) return false;
        var focused = ScreenManager.FocusedLayer;
        if (focused is GauntletLayer gauntlet &&
            gauntlet.UIContext.EventManager.FocusedWidget is EditableTextWidget) return false;
        return focused == null || focused == ((MapScreen)ScreenManager.TopScreen).SceneLayer;
    }

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
