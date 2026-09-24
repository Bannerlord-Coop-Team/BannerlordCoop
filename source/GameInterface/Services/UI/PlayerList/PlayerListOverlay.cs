using GameInterface.Services.Chat;
using SandBox.View.Map;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.UI.PlayerList;

/// <summary>Displays the roster over the campaign map without pausing the shared world.</summary>
internal sealed class PlayerListOverlay : GlobalLayer, IDisposable
{
    private readonly PlayerListVM viewModel;
    private readonly IChatService chat;
    private GauntletLayer layer;
    private GauntletMovieIdentifier movie;

    // Receives presentation state and the chat focus guard.
    public PlayerListOverlay(PlayerListVM viewModel, IChatService chat)
    {
        this.viewModel = viewModel;
        this.chat = chat;
    }

    // Keeps an unfocused layer available to receive the map toggle while closed.
    public void Initialize()
    {
        layer = new GauntletLayer("CoopPlayerList", 115);
        movie = layer.LoadMovie("CoopPlayerListUIMovie", viewModel);
        layer.InputRestrictions.ResetInputRestrictions();
        Layer = layer;
        ScreenManager.AddGlobalLayer(this, false);
    }

    // Closes outside gameplay and routes the same toggle used by the debug UI command.
    protected override void OnTick(float dt)
    {
        base.OnTick(dt);
        if (!IsMapAvailable())
        {
            Close();
            return;
        }
        if (viewModel.IsOpen && !ReferenceEquals(ScreenManager.FocusedLayer, layer))
        {
            Close();
            return;
        }
        if (Input.IsKeyPressed(InputKey.F8)) Toggle();
        else if (viewModel.IsOpen && Input.IsKeyReleased(InputKey.Escape)) Close();
    }

    // Requires the actual campaign map, not a map ticking behind a menu or mission.
    private bool IsMapAvailable() => ScreenManager.TopScreen is MapScreen &&
        GameStateManager.Current?.ActiveState is MapState map && !map.AtMenu &&
        !LoadingWindow.IsLoadingWindowActive && !InformationManager.IsAnyInquiryActive() &&
        Campaign.Current?.ConversationManager?.IsConversationInProgress != true;

    // Opens only when gameplay owns focus; never steals typing or another modal's input.
    public bool Toggle()
    {
        if (viewModel.IsOpen)
        {
            Close();
            return true;
        }
        if (!IsMapAvailable() || chat.IsTyping || Input.IsOnScreenKeyboardActive) return false;
        var focused = ScreenManager.FocusedLayer;
        if (focused is GauntletLayer gauntlet &&
            gauntlet.UIContext.EventManager.FocusedWidget is EditableTextWidget) return false;
        if (focused != null && focused != ((MapScreen)ScreenManager.TopScreen).SceneLayer) return false;
        viewModel.IsOpen = true;
        layer.InputRestrictions.SetInputRestrictions();
        layer.IsFocusLayer = true;
        ScreenManager.TrySetFocus(layer);
        return true;
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
