using System;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.GauntletUI.Data;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.Chat;

/// <summary>A global layer that remains available on both the campaign map and missions.</summary>
internal sealed class ChatOverlay : GlobalLayer, IDisposable
{
    private const string InputWidgetId = "CoopChatMessageInput";

    private readonly ChatVM dataSource;
    private readonly Action refreshParticipants;
    private GauntletLayer gauntletLayer;
    private GauntletMovieIdentifier movie;
    private EditableTextWidget inputWidget;
    private bool initialized;
    private bool isInputFocused;
    private bool ignoreNextOutsideClick;

    public ChatOverlay(ChatVM dataSource, Action refreshParticipants)
    {
        if (dataSource == null) throw new ArgumentNullException(nameof(dataSource));
        if (refreshParticipants == null) throw new ArgumentNullException(nameof(refreshParticipants));

        this.dataSource = dataSource;
        this.refreshParticipants = refreshParticipants;
        dataSource.OpenRequested += OpenInput;
        dataSource.CloseRequested += CloseInput;
    }

    public void Initialize()
    {
        if (initialized) return;

        gauntletLayer = new GauntletLayer("CoopChat", 900);
        movie = gauntletLayer.LoadMovie("CoopChatUIMovie", dataSource);
        SetPassiveInputRestrictions(gauntletLayer.InputRestrictions);
        Layer = gauntletLayer;
        ScreenManager.AddGlobalLayer(this, false);
        initialized = true;
    }

    protected override void OnTick(float dt)
    {
        base.OnTick(dt);
        if (!dataSource.IsOpen)
        {
            if (ShouldOpenInput(
                    Input.IsKeyPressed(InputKey.Enter),
                    Input.IsKeyPressed(InputKey.NumpadEnter),
                    Input.IsKeyPressed(InputKey.ControllerLOption)))
            {
                OpenInput();
            }
            return;
        }

        if (ShouldCloseInput(
                Input.IsKeyPressed(InputKey.Escape),
                Input.IsKeyPressed(InputKey.ControllerRRight),
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
        if (!initialized) return;

        CloseInput();
        if (movie != null) gauntletLayer.ReleaseMovie(movie);
        ScreenManager.RemoveGlobalLayer(this);
        dataSource.OnFinalize();

        inputWidget = null;
        movie = null;
        gauntletLayer = null;
        Layer = null;
        initialized = false;
    }

    private bool CanOpenInput()
    {
        if (Input.IsOnScreenKeyboardActive) return false;

        var focusedLayer = ScreenManager.FocusedLayer;
        if (focusedLayer == null || ReferenceEquals(focusedLayer, gauntletLayer)) return true;
        if (focusedLayer.InputRestrictions.Order > gauntletLayer.InputRestrictions.Order) return false;

        return focusedLayer is not GauntletLayer focusedGauntletLayer ||
               focusedGauntletLayer.UIContext.EventManager.FocusedWidget is not EditableTextWidget;
    }

    private void OpenInput()
    {
        if (!CanOpenInput()) return;

        refreshParticipants();
        dataSource.SetOpen(true);

        inputWidget ??= movie?.Movie?.RootWidget?
            .FindChild(InputWidgetId, includeAllChildren: true) as EditableTextWidget;
        ignoreNextOutsideClick = true;
        FocusInput();
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
        bool escapePressed,
        bool controllerCancelPressed,
        bool controllerTogglePressed)
    {
        return escapePressed || controllerCancelPressed || controllerTogglePressed;
    }

    internal static bool ShouldSendInput(
        bool enterPressed,
        bool numpadEnterPressed,
        bool controllerSendPressed)
    {
        return enterPressed || numpadEnterPressed || controllerSendPressed;
    }

    internal static void SetPassiveInputRestrictions(InputRestrictions inputRestrictions)
    {
        inputRestrictions.SetInputRestrictions(
            isMouseVisible: false,
            mask: InputUsageMask.Mouse);
    }
}
