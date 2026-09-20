using System;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.Chat;

/// <summary>Bottom-left event log and co-op chat UI shared by map and mission.</summary>
internal sealed class ChatOverlay : GlobalLayer, IDisposable
{
    private const string InputWidgetId = "CoopChatMessageInput";
    private const string FeedScrollablePanelId = "ChatFeedScrollablePanel";
    private const int LayerOrder = 110;

    private readonly ChatVM dataSource;
    private readonly Action refreshParticipants;
    private GauntletLayer gauntletLayer;
    private GauntletMovieIdentifier movie;
    private EditableTextWidget inputWidget;
    private ScrollablePanel feedScrollablePanel;
    private bool initialized;
    private bool isInputFocused;
    private bool ignoreNextOutsideClick;
    private bool playerChatEnabled;
    private bool pinFeedToBottom;
    private float pinFeedLastMaxValue = -1f;

    public ChatOverlay(ChatVM dataSource, Action refreshParticipants, bool playerChatEnabled)
    {
        if (dataSource == null) throw new ArgumentNullException(nameof(dataSource));
        if (refreshParticipants == null) throw new ArgumentNullException(nameof(refreshParticipants));

        this.dataSource = dataSource;
        this.refreshParticipants = refreshParticipants;
        this.playerChatEnabled = playerChatEnabled;
        dataSource.SetPlayerChatEnabled(playerChatEnabled);
        dataSource.OpenRequested += OpenInput;
        dataSource.CloseRequested += CloseInput;
        dataSource.FeedScrolledToBottomRequested += OnFeedScrolledToBottomRequested;
    }

    public void Initialize()
    {
        if (initialized) return;

        gauntletLayer = new GauntletLayer("CoopChat", LayerOrder);
        movie = gauntletLayer.LoadMovie("CoopChatUIMovie", dataSource);
        SetPassiveInputRestrictions(gauntletLayer.InputRestrictions);
        Layer = gauntletLayer;
        ScreenManager.AddGlobalLayer(this, false);
        initialized = true;
    }

    protected override void OnTick(float dt)
    {
        base.OnTick(dt);
        if (!UpdateVisibility()) return;

        dataSource.Tick(dt);

        if (!dataSource.IsOpen)
        {
            pinFeedToBottom = false;
            pinFeedLastMaxValue = -1f;
            if (playerChatEnabled && ShouldOpenInput(
                    Input.IsKeyPressed(InputKey.Enter),
                    Input.IsKeyPressed(InputKey.NumpadEnter),
                    Input.IsKeyPressed(InputKey.ControllerLOption)))
            {
                OpenInput();
            }
            return;
        }

        if (pinFeedToBottom)
            ContinuePinFeedToBottom();

        if (ShouldCaptureCloseInput(
                isInputFocused,
                Input.IsKeyPressed(InputKey.Escape),
                Input.IsKeyPressed(InputKey.ControllerRRight)))
            FocusInput();

        if (ShouldCloseInput(
                Input.IsKeyReleased(InputKey.Escape),
                Input.IsKeyReleased(InputKey.ControllerRRight),
                Input.IsKeyPressed(InputKey.ControllerLOption)))
        {
            CloseInput();
            return;
        }

        bool leftMousePressed = Input.IsKeyPressed(InputKey.LeftMouseButton);
        bool mouseButtonPressed = leftMousePressed ||
                                  Input.IsKeyPressed(InputKey.RightMouseButton) ||
                                  Input.IsKeyPressed(InputKey.MiddleMouseButton);
        bool inputHovered = inputWidget?.IsHovered == true;

        if (ignoreNextOutsideClick)
        {
            ignoreNextOutsideClick = false;
        }
        else if (!isInputFocused && leftMousePressed && inputHovered)
        {
            FocusInput();
        }
        else if (ShouldReleaseInputFocus(isInputFocused, mouseButtonPressed, inputHovered))
        {
            ReleaseInputFocus();
        }

        if (!isInputFocused) return;

        if (!ReferenceEquals(ScreenManager.FocusedLayer, gauntletLayer) ||
            !ReferenceEquals(gauntletLayer.UIContext.EventManager.FocusedWidget, inputWidget))
        {
            ReleaseInputFocus();
            return;
        }

        if (ShouldSendInput(
                Input.IsKeyPressed(InputKey.Enter),
                Input.IsKeyPressed(InputKey.NumpadEnter),
                Input.IsKeyPressed(InputKey.ControllerRLeft)))
            dataSource.ActionSend();
    }

    public void Dispose()
    {
        dataSource.OpenRequested -= OpenInput;
        dataSource.CloseRequested -= CloseInput;
        dataSource.FeedScrolledToBottomRequested -= OnFeedScrolledToBottomRequested;
        if (!initialized) return;

        CloseInput();
        if (movie != null) gauntletLayer.ReleaseMovie(movie);
        ScreenManager.RemoveGlobalLayer(this);
        dataSource.OnFinalize();

        inputWidget = null;
        feedScrollablePanel = null;
        movie = null;
        gauntletLayer = null;
        Layer = null;
        initialized = false;
    }

    internal bool IsPlayerChatEnabled => playerChatEnabled;

    internal void SetPlayerChatEnabled(bool value)
    {
        if (playerChatEnabled == value) return;

        playerChatEnabled = value;
        dataSource.SetPlayerChatEnabled(value);
        if (!value)
            CloseInput();
    }

    private bool CanOpenInput()
    {
        if (!playerChatEnabled || !gauntletLayer.IsActive || Input.IsOnScreenKeyboardActive) return false;

        var focusedLayer = ScreenManager.FocusedLayer;
        if (focusedLayer == null || ReferenceEquals(focusedLayer, gauntletLayer)) return true;
        if (focusedLayer.InputRestrictions.Order > gauntletLayer.InputRestrictions.Order) return false;

        return focusedLayer is not GauntletLayer focusedGauntletLayer ||
               focusedGauntletLayer.UIContext.EventManager.FocusedWidget is not EditableTextWidget;
    }

    private bool UpdateVisibility()
    {
        var topScreen = ScreenManager.TopScreen;
        ScreenLayer gameplayLayer;
        bool isGameplayScreen;
        bool isConversationActive = Campaign.Current?.ConversationManager?.IsConversationInProgress == true;

        if (topScreen is MapScreen mapScreen)
        {
            gameplayLayer = mapScreen.SceneLayer;
            isGameplayScreen = GameStateManager.Current?.ActiveState is MapState mapState && !mapState.AtMenu;
        }
        else if (topScreen is MissionScreen missionScreen)
        {
            gameplayLayer = missionScreen.SceneLayer;
            isGameplayScreen = true;
            isConversationActive |= missionScreen.IsConversationActive;
        }
        else
        {
            gameplayLayer = null;
            isGameplayScreen = false;
        }

        var focusedLayer = ScreenManager.FocusedLayer;
        bool shouldShow = ShouldShowPresentation(
            isGameplayScreen && !LoadingWindow.IsLoadingWindowActive,
            isConversationActive,
            ReferenceEquals(focusedLayer, gameplayLayer),
            ReferenceEquals(focusedLayer, gauntletLayer));

        if (gauntletLayer.IsActive == shouldShow) return shouldShow;

        if (!shouldShow) CloseInput();
        ScreenManager.SetSuspendLayer(gauntletLayer, !shouldShow);
        return shouldShow;
    }

    private void OpenInput()
    {
        if (!CanOpenInput()) return;

        refreshParticipants();
        dataSource.SetOpen(true);

        ResolveFeedWidgets();
        ignoreNextOutsideClick = true;
        FocusInput();
    }

    private void OnFeedScrolledToBottomRequested()
    {
        // ScrollablePanel updates MaxValue in OnLateUpdate after the new line is measured
        // One pin lands on the previous MaxValue (second-most-recent line) 
        // keep pinning until MaxValue stops growing
        pinFeedToBottom = true;
        pinFeedLastMaxValue = -1f;
    }

    private void ContinuePinFeedToBottom()
    {
        ResolveFeedWidgets();
        var bar = feedScrollablePanel?.VerticalScrollbar;
        if (bar == null) return;

        feedScrollablePanel.ResetTweenSpeed();
        float maxValue = bar.MaxValue;
        bar.ValueFloat = maxValue;

        if (pinFeedLastMaxValue >= 0f && maxValue <= pinFeedLastMaxValue + 0.01f)
        {
            pinFeedToBottom = false;
            pinFeedLastMaxValue = -1f;
            return;
        }

        pinFeedLastMaxValue = maxValue;
    }

    private void ResolveFeedWidgets()
    {
        var root = movie?.Movie?.RootWidget;
        if (root == null) return;

        inputWidget ??= root.FindChild(InputWidgetId, includeAllChildren: true) as EditableTextWidget;
        feedScrollablePanel ??= root.FindChild(FeedScrollablePanelId, includeAllChildren: true) as ScrollablePanel;
    }

    private void CloseInput()
    {
        if (gauntletLayer == null) return;

        dataSource.SetOpen(false);
        ignoreNextOutsideClick = false;
        ReleaseInputFocus();
    }

    private void FocusInput()
    {
        if (inputWidget == null) return;

        gauntletLayer.InputRestrictions.SetInputRestrictions();
        gauntletLayer.IsFocusLayer = true;
        ScreenManager.TrySetFocus(gauntletLayer);
        if (!ReferenceEquals(ScreenManager.FocusedLayer, gauntletLayer))
        {
            gauntletLayer.IsFocusLayer = false;
            SetPassiveInputRestrictions(gauntletLayer.InputRestrictions);
            return;
        }

        gauntletLayer.UIContext.EventManager.FocusedWidget = inputWidget;
        isInputFocused = true;
    }

    private void ReleaseInputFocus()
    {
        gauntletLayer.UIContext.EventManager.FocusedWidget = null;

        isInputFocused = false;
        gauntletLayer.IsFocusLayer = false;
        ScreenManager.TryLoseFocus(gauntletLayer);
        SetPassiveInputRestrictions(gauntletLayer.InputRestrictions);
    }

    internal static bool ShouldReleaseInputFocus(
        bool inputFocused,
        bool mouseButtonPressed,
        bool inputHovered)
    {
        return inputFocused && mouseButtonPressed && !inputHovered;
    }

    internal static bool ShouldOpenInput(
        bool enterPressed,
        bool numpadEnterPressed,
        bool controllerOpenPressed)
    {
        return enterPressed || numpadEnterPressed || controllerOpenPressed;
    }

    internal static bool ShouldCloseInput(
        bool escapeReleased,
        bool controllerCancelReleased,
        bool controllerTogglePressed)
    {
        return escapeReleased || controllerCancelReleased || controllerTogglePressed;
    }

    internal static bool ShouldCaptureCloseInput(
        bool inputFocused,
        bool escapePressed,
        bool controllerCancelPressed)
    {
        return !inputFocused &&
               (escapePressed || controllerCancelPressed);
    }

    internal static bool ShouldSendInput(
        bool enterPressed,
        bool numpadEnterPressed,
        bool controllerSendPressed)
    {
        return enterPressed || numpadEnterPressed || controllerSendPressed;
    }

    internal static bool ShouldShowPresentation(
        bool isGameplayScreen,
        bool isConversationActive,
        bool isGameplayLayerFocused,
        bool isChatLayerFocused)
    {
        return isGameplayScreen &&
               !isConversationActive &&
               (isGameplayLayerFocused || isChatLayerFocused);
    }

    internal static void SetPassiveInputRestrictions(InputRestrictions inputRestrictions)
    {
        inputRestrictions.SetInputRestrictions(
            isMouseVisible: false,
            mask: InputUsageMask.Mouse);
    }
}