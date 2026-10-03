using System;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
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
    private const string ChatRootWidgetId = "CoopChatRoot";
    private const string OpenBackdropWidgetId = "CoopChatOpenBackdrop";
    private const string FeedScrollablePanelId = "ChatFeedScrollablePanel";
    private const string ResizerWidgetId = "CoopChatResizer";
    private const string ResizeFrameWidgetId = "CoopChatResizeFrame";
    private const string ResizeCaptureWidgetId = "CoopChatResizeCapture";
    private const int LayerOrder = 200;
    private const float ResizeTransitionSeconds = 0.14f;

    private readonly ChatVM dataSource;
    private readonly Action refreshParticipants;
    private readonly IChatVanillaLogGate vanillaLogGate;
    private GauntletLayer gauntletLayer;
    private GauntletMovieIdentifier movie;
    private EditableTextWidget inputWidget;
    private Widget chatRootWidget;
    private Widget openBackdropWidget;
    private ScrollablePanel feedScrollablePanel;
    private Widget resizerWidget;
    private Widget resizeFrameWidget;
    private Widget resizeCaptureWidget;
    private bool initialized;
    private bool isInputFocused;
    private bool ignoreNextOutsideClick;
    private bool playerChatEnabled;
    private bool allowChatOpen;
    private bool pinFeedToBottom;
    private float pinFeedLastMaxValue = -1f;
    private bool isResizing;
    private bool applyResizeToPanel;
    private bool feedInnerPoliciesCaptured;
    private float resizeLerpRatio;
    private Vec2 resizeStartMousePosition;
    private Vec2 resizeOriginalSize;
    private SizePolicy feedInnerWidthPolicy;
    private SizePolicy feedInnerHeightPolicy;

    public ChatOverlay(
        ChatVM dataSource,
        Action refreshParticipants,
        bool playerChatEnabled,
        IChatVanillaLogGate vanillaLogGate)
    {
        if (dataSource == null) throw new ArgumentNullException(nameof(dataSource));
        if (refreshParticipants == null) throw new ArgumentNullException(nameof(refreshParticipants));
        if (vanillaLogGate == null) throw new ArgumentNullException(nameof(vanillaLogGate));

        this.dataSource = dataSource;
        this.refreshParticipants = refreshParticipants;
        this.vanillaLogGate = vanillaLogGate;
        this.playerChatEnabled = playerChatEnabled;
        dataSource.SetPlayerChatEnabled(playerChatEnabled);
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
            CancelActiveResize();
            // Keep pinning while closed so new lines stay in view
            if (pinFeedToBottom)
                ContinuePinFeedToBottom();
            if (allowChatOpen && playerChatEnabled && ShouldOpenInput(
                    Input.IsKeyPressed(InputKey.Enter),
                    Input.IsKeyPressed(InputKey.NumpadEnter),
                    Input.IsKeyPressed(InputKey.ControllerLOption)))
            {
                OpenInput();
            }
            return;
        }

        UpdateResize(dt);
        UpdateOpenPanelCursorAndCapture();

        if (pinFeedToBottom)
            ContinuePinFeedToBottom();

        if (isResizing || applyResizeToPanel) return;

        if (isInputFocused && !dataSource.IsChatInputEnabled)
            ReleaseInputFocus();

        if (ShouldCaptureCloseInput(
                isInputFocused,
                Input.IsKeyPressed(InputKey.Escape),
                Input.IsKeyPressed(InputKey.ControllerRRight)))
        {
            // Events don't have text focus so close on press
            if (!dataSource.IsChatInputEnabled)
            {
                ClaimOpenPanelFocusForClose();
                CloseInput();
                return;
            }

            FocusInput();
        }

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
        dataSource.FeedScrolledToBottomRequested -= OnFeedScrolledToBottomRequested;
        if (!initialized) return;

        CloseInput();
        if (movie != null) gauntletLayer.ReleaseMovie(movie);
        ScreenManager.RemoveGlobalLayer(this);
        dataSource.OnFinalize();

        inputWidget = null;
        chatRootWidget = null;
        openBackdropWidget = null;
        feedScrollablePanel = null;
        resizerWidget = null;
        resizeFrameWidget = null;
        resizeCaptureWidget = null;
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
        if (!allowChatOpen || !playerChatEnabled || !gauntletLayer.IsActive || Input.IsOnScreenKeyboardActive)
            return false;

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
        bool isCampaignContext = Campaign.Current != null;
        bool isLoading = LoadingWindow.IsLoadingWindowActive;

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
            // Character, clan, party, inventory, settlement menus, Coop Options, etc.
            isGameplayScreen = false;
        }

        var focusedLayer = ScreenManager.FocusedLayer;
        bool isGameplayLayerFocused = ReferenceEquals(focusedLayer, gameplayLayer);
        bool isChatLayerFocused = ReferenceEquals(focusedLayer, gauntletLayer);

        // Don't fall back to vanilla feed when gameplay screens clear the frame
        bool shouldShow = ShouldShowPresentation(isCampaignContext, isConversationActive, isLoading);
        allowChatOpen = ShouldAllowChatOpen(
            isGameplayScreen && !isLoading,
            isConversationActive,
            isGameplayLayerFocused,
            isChatLayerFocused);

        if (!allowChatOpen && dataSource.IsOpen)
            CloseInput();

        if (gauntletLayer.IsActive == shouldShow)
        {
            vanillaLogGate.SetReplacementVisible(shouldShow);
            return shouldShow;
        }

        if (!shouldShow) CloseInput();
        ScreenManager.SetSuspendLayer(gauntletLayer, !shouldShow);
        vanillaLogGate.SetReplacementVisible(shouldShow);
        return shouldShow;
    }

    private void OpenInput()
    {
        if (!CanOpenInput()) return;

        refreshParticipants();
        dataSource.SetOpen(true);

        ResolveFeedWidgets();
        SetOpenPanelEventAcceptance(true);
        ignoreNextOutsideClick = true;
        FocusInput();
    }

    private void OnFeedScrolledToBottomRequested()
    {
        // MaxValue updates in OnLateUpdate after the line is measured so pin until it settles
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

    private void UpdateResize(float dt)
    {
        ResolveFeedWidgets();
        if (resizerWidget == null || resizeFrameWidget == null) return;

        if (Input.IsKeyPressed(InputKey.LeftMouseButton) &&
            ReferenceEquals(gauntletLayer.UIContext.EventManager.HoveredWidget, resizerWidget))
        {
            // Full focus release so gameplay keys work after resize; keep open-panel cursor
            ReleaseInputFocus();
            // Finish any active Fixed policies before taking a new snapshot
            if (applyResizeToPanel || feedInnerPoliciesCaptured)
                RestoreFeedInnerPolicies();
            applyResizeToPanel = false;

            isResizing = true;
            SetResizeCaptureVisible(true);
            resizeStartMousePosition = Input.MousePositionPixel;
            resizeOriginalSize = new Vec2(dataSource.ChatBoxSizeX, dataSource.ChatBoxSizeY);
            resizeFrameWidget.IsVisible = true;
            resizeFrameWidget.WidthSizePolicy = SizePolicy.Fixed;
            resizeFrameWidget.HeightSizePolicy = SizePolicy.Fixed;
            resizeFrameWidget.SuggestedWidth = dataSource.ChatBoxSizeX;
            resizeFrameWidget.SuggestedHeight = dataSource.ChatBoxSizeY;

            CaptureAndFreezeFeedInnerPanel();
        }
        else if (Input.IsKeyReleased(InputKey.LeftMouseButton))
        {
            if (isResizing)
            {
                resizeFrameWidget.IsVisible = false;
                applyResizeToPanel = true;
                resizeLerpRatio = 0f;
                SetResizeCaptureVisible(false);
            }

            isResizing = false;
        }

        if (isResizing)
        {
            Vec2 mouseDelta = Input.MousePositionPixel - resizeStartMousePosition;
            Vec2 proposed = resizeOriginalSize + new Vec2(mouseDelta.X, -mouseDelta.Y);
            resizeFrameWidget.SuggestedWidth = ChatVM.ClampSizeX(proposed.X);
            resizeFrameWidget.SuggestedHeight = ChatVM.ClampSizeY(proposed.Y);
        }
        else if (applyResizeToPanel)
        {
            resizeLerpRatio = MBMath.ClampFloat(resizeLerpRatio + (dt / ResizeTransitionSeconds), 0f, 1f);
            float targetWidth = resizeFrameWidget.SuggestedWidth;
            float targetHeight = resizeFrameWidget.SuggestedHeight;
            dataSource.ChatBoxSizeX = MBMath.Lerp(resizeOriginalSize.X, targetWidth, resizeLerpRatio);
            dataSource.ChatBoxSizeY = MBMath.Lerp(resizeOriginalSize.Y, targetHeight, resizeLerpRatio);

            if (Math.Abs(dataSource.ChatBoxSizeX - targetWidth) < 0.01f &&
                Math.Abs(dataSource.ChatBoxSizeY - targetHeight) < 0.01f)
            {
                dataSource.ChatBoxSizeX = targetWidth;
                dataSource.ChatBoxSizeY = targetHeight;
                resizeFrameWidget.WidthSizePolicy = SizePolicy.StretchToParent;
                resizeFrameWidget.HeightSizePolicy = SizePolicy.StretchToParent;
                RestoreFeedInnerPolicies();
                applyResizeToPanel = false;
            }
        }
    }

    private void CaptureAndFreezeFeedInnerPanel()
    {
        if (feedScrollablePanel?.InnerPanel == null) return;

        feedInnerWidthPolicy = feedScrollablePanel.InnerPanel.WidthSizePolicy;
        feedInnerHeightPolicy = feedScrollablePanel.InnerPanel.HeightSizePolicy;
        feedInnerPoliciesCaptured = true;
        feedScrollablePanel.InnerPanel.WidthSizePolicy = SizePolicy.Fixed;
        feedScrollablePanel.InnerPanel.HeightSizePolicy = SizePolicy.Fixed;
        feedScrollablePanel.InnerPanel.SuggestedWidth = feedScrollablePanel.InnerPanel.Size.X;
        feedScrollablePanel.InnerPanel.SuggestedHeight = feedScrollablePanel.InnerPanel.Size.Y;
    }

    private void RestoreFeedInnerPolicies()
    {
        if (!feedInnerPoliciesCaptured) return;

        ResolveFeedWidgets();
        if (feedScrollablePanel?.InnerPanel != null)
        {
            feedScrollablePanel.InnerPanel.WidthSizePolicy = feedInnerWidthPolicy;
            feedScrollablePanel.InnerPanel.HeightSizePolicy = feedInnerHeightPolicy;
        }

        feedInnerPoliciesCaptured = false;
    }

    private void CancelActiveResize()
    {
        RestoreFeedInnerPolicies();
        SetResizeCaptureVisible(false);
        isResizing = false;
        applyResizeToPanel = false;
        if (resizeFrameWidget == null) return;

        resizeFrameWidget.IsVisible = false;
        resizeFrameWidget.WidthSizePolicy = SizePolicy.StretchToParent;
        resizeFrameWidget.HeightSizePolicy = SizePolicy.StretchToParent;
    }

    private void ResolveFeedWidgets()
    {
        var root = movie?.Movie?.RootWidget;
        if (root == null) return;

        inputWidget ??= root.FindChild(InputWidgetId, includeAllChildren: true) as EditableTextWidget;
        chatRootWidget ??= root.FindChild(ChatRootWidgetId, includeAllChildren: true);
        openBackdropWidget ??= root.FindChild(OpenBackdropWidgetId, includeAllChildren: true);
        feedScrollablePanel ??= root.FindChild(FeedScrollablePanelId, includeAllChildren: true) as ScrollablePanel;
        resizerWidget ??= root.FindChild(ResizerWidgetId, includeAllChildren: true);
        resizeFrameWidget ??= root.FindChild(ResizeFrameWidgetId, includeAllChildren: true);
        resizeCaptureWidget ??= root.FindChild(ResizeCaptureWidgetId, includeAllChildren: true);
    }

    private void SetResizeCaptureVisible(bool visible)
    {
        ResolveFeedWidgets();
        if (resizeCaptureWidget != null)
            resizeCaptureWidget.IsVisible = visible;
    }

    /// <summary>
    /// Closed feed must stay click-through
    /// While open the backdrop has control of mouse clicks and wheel for scrolling
    /// </summary>
    private void SetOpenPanelEventAcceptance(bool acceptEvents)
    {
        ResolveFeedWidgets();
        bool doNotAccept = !acceptEvents;
        if (openBackdropWidget != null)
            openBackdropWidget.DoNotAcceptEvents = doNotAccept;
        if (feedScrollablePanel != null)
            feedScrollablePanel.DoNotAcceptEvents = doNotAccept;
    }

    private void UpdateOpenPanelCursorAndCapture()
    {
        if (gauntletLayer == null) return;

        ResolveFeedWidgets();
        bool holdFromChat = IsMouseHoldFromChat();
        // Resize owns the capture widget while dragging
        if (!isResizing && !applyResizeToPanel)
            SetResizeCaptureVisible(holdFromChat);

        // Typing path already claimed keyboard via FocusInput
        if (isInputFocused) return;

        bool overChat = IsPointerOverOpenChat();
        bool mapLookActive = Input.IsKeyDown(InputKey.RightMouseButton) && !holdFromChat && !overChat;
        bool showCursor = ShouldShowOpenPanelCursor(
            inputFocused: false,
            pointerOverChat: overChat,
            mouseCaptureActive: holdFromChat || isResizing || applyResizeToPanel,
            mapLookActive: mapLookActive);

        // While open, keep the cursor up for tabs/resize; hide only during map RMB look
        if (showCursor)
            SetOpenPanelInputRestrictions(gauntletLayer.InputRestrictions);
        else
            gauntletLayer.InputRestrictions.SetInputRestrictions(false, InputUsageMask.Mouse);
    }

    private bool IsMouseHoldFromChat()
    {
        bool lmbDown = Input.IsKeyDown(InputKey.LeftMouseButton);
        bool rmbDown = Input.IsKeyDown(InputKey.RightMouseButton);
        if (!lmbDown && !rmbDown) return false;

        var eventManager = gauntletLayer.UIContext.EventManager;
        if (lmbDown && IsUnderChatRoot(eventManager.LatestMouseDownWidget))
            return true;
        if (rmbDown && IsUnderChatRoot(eventManager.LatestMouseAlternateDownWidget))
            return true;

        return isResizing;
    }

    private bool IsPointerOverOpenChat()
    {
        if (chatRootWidget == null) return false;

        var eventManager = gauntletLayer.UIContext.EventManager;
        if (IsUnderChatRoot(eventManager.HoveredWidget))
            return true;

        return chatRootWidget.IsPointInsideMeasuredArea(eventManager.MousePosition);
    }

    private bool IsUnderChatRoot(Widget widget)
    {
        if (chatRootWidget == null || widget == null) return false;

        for (Widget current = widget; current != null; current = current.ParentWidget)
        {
            if (ReferenceEquals(current, chatRootWidget))
                return true;
        }

        return false;
    }

    private void CloseInput()
    {
        if (gauntletLayer == null) return;

        dataSource.SetOpen(false);
        ignoreNextOutsideClick = false;
        SetOpenPanelEventAcceptance(false);
        CancelActiveResize();
        ReleaseInputFocus();
    }

    private void FocusInput()
    {
        if (inputWidget == null) return;

        // Events (read-only): show cursor for tabs/resize without focusing the disabled input
        if (!dataSource.IsChatInputEnabled)
        {
            SetOpenPanelInputRestrictions(gauntletLayer.InputRestrictions);
            return;
        }

        gauntletLayer.InputRestrictions.SetInputRestrictions();
        gauntletLayer.IsFocusLayer = true;
        ScreenManager.TrySetFocus(gauntletLayer);
        if (!ReferenceEquals(ScreenManager.FocusedLayer, gauntletLayer))
        {
            gauntletLayer.IsFocusLayer = false;
            SetOpenPanelInputRestrictions(gauntletLayer.InputRestrictions);
            return;
        }

        gauntletLayer.UIContext.EventManager.FocusedWidget = inputWidget;
        isInputFocused = true;
    }

    /// <summary>Briefly own Escape so the game menu does not open, then CloseInput clears it.</summary>
    private void ClaimOpenPanelFocusForClose()
    {
        gauntletLayer.InputRestrictions.SetInputRestrictions();
        gauntletLayer.IsFocusLayer = true;
        ScreenManager.TrySetFocus(gauntletLayer);
    }

    private void ReleaseInputFocus()
    {
        gauntletLayer.UIContext.EventManager.FocusedWidget = null;

        isInputFocused = false;
        gauntletLayer.IsFocusLayer = false;
        ScreenManager.TryLoseFocus(gauntletLayer);
        if (dataSource.IsOpen)
        {
            // Cursor visibility for the open panel is refreshed next tick
            SetOpenPanelInputRestrictions(gauntletLayer.InputRestrictions);
        }
        else
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

    /// <summary>Passive event feed for any campaign screen (map, party, character, settlement, …).</summary>
    internal static bool ShouldShowPresentation(
        bool isCampaignContext,
        bool isConversationActive,
        bool isLoading)
    {
        return isCampaignContext && !isConversationActive && !isLoading;
    }

    /// <summary>Enter/typing only on unobstructed map or mission gameplay.</summary>
    internal static bool ShouldAllowChatOpen(
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

    internal static void SetOpenPanelInputRestrictions(InputRestrictions inputRestrictions)
    {
        inputRestrictions.SetInputRestrictions(
            isMouseVisible: true,
            mask: InputUsageMask.Mouse);
    }

    /// <summary>
    /// Open panel keeps a cursor for UI, except during map RMB look which would warp a visible cursor.
    /// </summary>
    internal static bool ShouldShowOpenPanelCursor(
        bool inputFocused,
        bool pointerOverChat,
        bool mouseCaptureActive,
        bool mapLookActive)
    {
        if (inputFocused || pointerOverChat || mouseCaptureActive)
            return true;
        return !mapLookActive;
    }
}