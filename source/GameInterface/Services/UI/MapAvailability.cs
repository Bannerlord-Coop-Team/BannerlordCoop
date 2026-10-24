using GameInterface.Services.Chat;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.UI;

/// <summary>Tells the map overlays whether the campaign map is free for them to show.</summary>
public interface IMapAvailability
{
    /// <summary>True on the actual campaign map, not a map ticking behind a menu, mission, loading window, inquiry or conversation.</summary>
    bool IsMapAvailable();

    /// <summary>True when an overlay may take focus: the map is available and no chat input, text field or other layer has it.</summary>
    bool CanOpenOverlay();
}

/// <inheritdoc cref="IMapAvailability"/>
internal sealed class MapAvailability : IMapAvailability
{
    private readonly IChatService chat;

    public MapAvailability(IChatService chat)
    {
        this.chat = chat;
    }

    public bool IsMapAvailable() => ScreenManager.TopScreen is MapScreen &&
        GameStateManager.Current?.ActiveState is MapState map && !map.AtMenu &&
        !LoadingWindow.IsLoadingWindowActive && !InformationManager.IsAnyInquiryActive() &&
        Campaign.Current?.ConversationManager?.IsConversationInProgress != true;

    // One guard for the player list and the server info panel, so neither steals typing or another modal's input.
    public bool CanOpenOverlay()
    {
        if (!IsMapAvailable() || chat.IsTyping || Input.IsOnScreenKeyboardActive) return false;
        var focused = ScreenManager.FocusedLayer;
        if (focused is GauntletLayer gauntlet &&
            gauntlet.UIContext.EventManager.FocusedWidget is EditableTextWidget) return false;
        return focused == null || focused == ((MapScreen)ScreenManager.TopScreen).SceneLayer;
    }
}
