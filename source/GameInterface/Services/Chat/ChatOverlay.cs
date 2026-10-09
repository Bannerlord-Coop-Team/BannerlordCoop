using System;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
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
    /// <summary>True while a press that started over the open chat panel is still held.</summary>
    private bool chatPointerCaptureHeld;
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

        // Unfocused open panel: Enter returns to typing without claiming keys until then
        if (!isInputFocused)
        {
            if (dataSource.IsChatInputEnabled &&
                ShouldOpenInput(
                    Input.IsKeyPressed(InputKey.Enter),
                    Input.IsKeyPressed(InputKey.NumpadEnter),
                    controllerOpenPressed: false))
            {
                FocusInput();
            }

            return;
        }

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

    /// <summary>True only while the text field owns keyboard focus (panel open alone does not count).</summary>
    internal bool IsInputFocused => isInputFocused;

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

        // Don't steal from another text field
        if (focusedLayer is GauntletLayer focusedGauntletLayer &&
            focusedGauntletLayer.UIContext.EventManager.FocusedWidget is EditableTextWidget)
        {
            return false;
        }

        // Settlement game menus often sit above this global layer; still allow Enter there
        if (focusedLayer.InputRestrictions.Order > gauntletLayer.InputRestrictions.Order &&
            !IsSettlementMapMenuActive())
        {
            return false;
        }

        return true;
    }

    private bool UpdateVisibility()
    {
        var topScreen = ScreenManager.TopScreen;
        ScreenLayer gameplayLayer;
        bool isOpenableScreen;
        bool isSettlementMenu = false;
        bool isConversationActive = Campaign.Current?.ConversationManager?.IsConversationInProgress == true;
        bool isCampaignContext = Campaign.Current != null;
        bool isLoading = LoadingWindow.IsLoadingWindowActive;

        if (topScreen is MapScreen mapScreen)
        {
            gameplayLayer = mapScreen.SceneLayer;
            var mapState = GameStateManager.Current?.ActiveState as MapState;
            bool atMenu = mapState?.AtMenu == true;
            bool hasSettlement = MobileParty.MainParty?.CurrentSettlement != null;
            isSettlementMenu = IsSettlementMapMenu(isMapScreen: true, atMenu, hasSettlement);
            // Open map, or settlement town/castle/village menus (not inventory/party — those leave MapScreen)
            isOpenableScreen = mapState != null && (!atMenu || isSettlementMenu);
        }
        else if (topScreen is MissionScreen missionScreen)
        {
            gameplayLayer = missionScreen.SceneLayer;
            isOpenableScreen = true;
            isConversationActive |= missionScreen.IsConversationActive;
        }
        else
        {
            gameplayLayer = null;
            // Character, clan, party, inventory, Coop Options, etc.
            isOpenableScreen = false;
        }

        var focusedLayer = ScreenManager.FocusedLayer;
        bool isGameplayLayerFocused = ReferenceEquals(focusedLayer, gameplayLayer);
        bool isChatLayerFocused = ReferenceEquals(focusedLayer, gauntletLayer);
        bool isSettlementMenuLayerFocused = isSettlementMenu &&
            topScreen is MapScreen settlementMapScreen &&
            IsLayerOnScreen(settlementMapScreen, focusedLayer);

        // Don't fall back to vanilla feed when gameplay screens clear the frame
        bool shouldShow = ShouldShowPresentation(isCampaignContext, isConversationActive, isLoading);
        allowChatOpen = ShouldAllowChatOpen(
            isOpenableScreen && !isLoading,
            isConversationActive,
            isGameplayLayerFocused || isSettlementMenuLayerFocused,
            isChatLayerFocused);

        if (!allowChatOpen && dataSource.IsOpen)
            CloseInput();

        if (gauntletLayer.IsActive != shouldShow)
        {
            if (!shouldShow) CloseInput();
            ScreenManager.SetSuspendLayer(gauntletLayer, !shouldShow);
        }

        vanillaLogGate.SetReplacementVisible(shouldShow);
        // Menus/options sit under this global layer; drop mouse so their widgets stay clickable
        if (shouldShow && !dataSource.IsOpen)
            ApplyClosedFeedInputRestrictions(isSettlementMenu);

        return shouldShow;
    }

    private void ApplyClosedFeedInputRestrictions(bool settlementMenu = false)
    {
        // Settlement menus need display-only even when Enter is allowed, otherwise the feed steals clicks
        if (allowChatOpen && !settlementMenu)
            SetPassiveInputRestrictions(gauntletLayer.InputRestrictions);
        else
            SetDisplayOnlyInputRestrictions(gauntletLayer.InputRestrictions);
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
        bool overChat = IsPointerOverOpenChat();
        // Do not use LatestMouseDownWidget — it stays on the last chat control after DisplayOnly
        // map clicks, which would re-open the fullscreen capture and steal movement.
        bool holdFromChat = UpdateChatPointerCapture(overChat);
        // Resize owns the capture widget while dragging
        if (!isResizing && !applyResizeToPanel)
            SetResizeCaptureVisible(holdFromChat);

        bool mouseCaptureActive = holdFromChat || isResizing || applyResizeToPanel;
        // Only claim mouse over the panel (or while dragging it); otherwise map clicks pass through
        bool claimMouse = overChat || mouseCaptureActive;

        if (isInputFocused)
        {
            if (claimMouse)
                SetOpenPanelTypingWithMouseRestrictions(gauntletLayer.InputRestrictions);
            else
                SetOpenPanelTypingRestrictions(gauntletLayer.InputRestrictions);
            return;
        }

        if (claimMouse)
            SetOpenPanelInputRestrictions(gauntletLayer.InputRestrictions);
        else
            SetDisplayOnlyInputRestrictions(gauntletLayer.InputRestrictions);
    }

    private bool UpdateChatPointerCapture(bool pointerOverChat)
    {
        bool buttonDown = Input.IsKeyDown(InputKey.LeftMouseButton) ||
                          Input.IsKeyDown(InputKey.RightMouseButton);
        bool buttonPressed = Input.IsKeyPressed(InputKey.LeftMouseButton) ||
                             Input.IsKeyPressed(InputKey.RightMouseButton);

        chatPointerCaptureHeld = ShouldKeepChatPointerCapture(
            chatPointerCaptureHeld,
            buttonDown,
            buttonPressed,
            pointerOverChat);
        return chatPointerCaptureHeld;
    }

    private bool IsPointerOverOpenChat()
    {
        if (chatRootWidget == null) return false;

        // Measured bounds only — avoid Gauntlet hover, which can go stale under DisplayOnly.
        return chatRootWidget.IsPointInsideMeasuredArea(
            gauntletLayer.UIContext.EventManager.MousePosition);
    }

    private void CloseInput()
    {
        if (gauntletLayer == null) return;

        dataSource.SetOpen(false);
        ignoreNextOutsideClick = false;
        chatPointerCaptureHeld = false;
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

        // Keyboard only here; UpdateOpenPanelCursorAndCapture adds mouse when the pointer is over chat
        SetOpenPanelTypingRestrictions(gauntletLayer.InputRestrictions);
        gauntletLayer.IsFocusLayer = true;
        ScreenManager.TrySetFocus(gauntletLayer);
        if (!ReferenceEquals(ScreenManager.FocusedLayer, gauntletLayer))
        {
            gauntletLayer.IsFocusLayer = false;
            UpdateOpenPanelCursorAndCapture();
            return;
        }

        gauntletLayer.UIContext.EventManager.FocusedWidget = inputWidget;
        isInputFocused = true;
        UpdateOpenPanelCursorAndCapture();
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
            UpdateOpenPanelCursorAndCapture();
        else
            ApplyClosedFeedInputRestrictions(IsSettlementMapMenuActive());
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

    /// <summary>Enter/typing on open map, settlement map menus, or mission gameplay.</summary>
    internal static bool ShouldAllowChatOpen(
        bool isOpenableScreen,
        bool isConversationActive,
        bool isGameplayLayerFocused,
        bool isChatLayerFocused)
    {
        return isOpenableScreen &&
               !isConversationActive &&
               (isGameplayLayerFocused || isChatLayerFocused);
    }

    /// <summary>Town/castle/village game menus on MapScreen; inventory/party replace the screen and stay excluded.</summary>
    internal static bool IsSettlementMapMenu(bool isMapScreen, bool atMenu, bool hasCurrentSettlement)
    {
        return isMapScreen && atMenu && hasCurrentSettlement;
    }

    private static bool IsSettlementMapMenuActive()
    {
        if (ScreenManager.TopScreen is not MapScreen)
            return false;

        var mapState = GameStateManager.Current?.ActiveState as MapState;
        return IsSettlementMapMenu(
            isMapScreen: true,
            atMenu: mapState?.AtMenu == true,
            hasCurrentSettlement: MobileParty.MainParty?.CurrentSettlement != null);
    }

    private static bool IsLayerOnScreen(ScreenBase screen, ScreenLayer layer)
    {
        if (screen == null || layer == null) return false;

        var layers = screen.Layers;
        for (int i = 0; i < layers.Count; i++)
        {
            if (ReferenceEquals(layers[i], layer))
                return true;
        }

        return false;
    }

    /// <summary>Closed feed on map/mission: mouse mask for hit-testing, widgets stay click-through.</summary>
    internal static void SetPassiveInputRestrictions(InputRestrictions inputRestrictions)
    {
        inputRestrictions.SetInputRestrictions(
            isMouseVisible: false,
            mask: InputUsageMask.Mouse);
    }

    /// <summary>Closed feed over menus/options: no mouse so lower Gauntlet screens receive clicks.</summary>
    internal static void SetDisplayOnlyInputRestrictions(InputRestrictions inputRestrictions)
    {
        inputRestrictions.SetInputRestrictions(
            isMouseVisible: false,
            mask: InputUsageMask.Invalid);
    }

    internal static void SetOpenPanelInputRestrictions(InputRestrictions inputRestrictions)
    {
        inputRestrictions.SetInputRestrictions(
            isMouseVisible: true,
            mask: InputUsageMask.Mouse);
    }

    /// <summary>Typing with the pointer off the panel: keep keys, leave mouse to the map.</summary>
    internal static void SetOpenPanelTypingRestrictions(InputRestrictions inputRestrictions)
    {
        inputRestrictions.SetInputRestrictions(
            isMouseVisible: false,
            mask: InputUsageMask.Keyboardkeys);
    }

    /// <summary>Typing while interacting with the open panel chrome.</summary>
    internal static void SetOpenPanelTypingWithMouseRestrictions(InputRestrictions inputRestrictions)
    {
        inputRestrictions.SetInputRestrictions(
            isMouseVisible: true,
            mask: InputUsageMask.All);
    }

    /// <summary>
    /// Capture starts only on a fresh press over the panel. LatestMouseDownWidget must not be used:
    /// it survives DisplayOnly map clicks and would treat every later map click as a chat hold.
    /// </summary>
    internal static bool ShouldKeepChatPointerCapture(
        bool currentlyHeld,
        bool buttonDown,
        bool buttonPressed,
        bool pointerOverChat)
    {
        if (!buttonDown) return false;
        if (currentlyHeld) return true;
        return buttonPressed && pointerOverChat;
    }
}