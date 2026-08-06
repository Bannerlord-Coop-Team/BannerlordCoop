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
        this.dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        this.refreshParticipants = refreshParticipants ?? throw new ArgumentNullException(nameof(refreshParticipants));
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
        if (!dataSource.IsOpen) return;

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

        if (Input.IsKeyPressed(InputKey.Escape))
            CloseInput();
        else if (Input.IsKeyPressed(InputKey.Enter) || Input.IsKeyPressed(InputKey.NumpadEnter))
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
        if (inputWidget == null || gauntletLayer.UIContext.EventManager.IsControllerActive) return;

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

    internal static void SetPassiveInputRestrictions(InputRestrictions inputRestrictions)
    {
        inputRestrictions.SetInputRestrictions(
            isMouseVisible: false,
            mask: InputUsageMask.Mouse);
    }
}
