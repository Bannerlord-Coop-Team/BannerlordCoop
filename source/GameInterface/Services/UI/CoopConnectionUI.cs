using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.UI;

public class CoopConnectionUI : ScreenBase
{
    private const string SteamLobbyHostSearchInputId = "SteamLobbyHostSearchInput";

    private CoopConnectMenuVM _dataSource;
    private GauntletLayer _gauntletLayer;
    private GauntletMovieIdentifier _gauntletMovie;
    private bool _focusSteamLobbyHostSearchOnNextFrame;

    protected override void OnInitialize()
    {
        base.OnInitialize();
        _dataSource = new CoopConnectMenuVM();
        _gauntletLayer = new GauntletLayer("CoopConnectionUI", 100)
        {
            IsFocusLayer = true
        };
        AddLayer(_gauntletLayer);
        _gauntletLayer.InputRestrictions.SetInputRestrictions();
        _gauntletMovie = _gauntletLayer.LoadMovie("CoopConnectionUIMovie", _dataSource);
        _dataSource.SteamLobbiesTabActivated += QueueSteamLobbyHostSearchFocus;
    }

    protected override void OnActivate()
    {
        base.OnActivate();
        ScreenManager.TrySetFocus(_gauntletLayer);
    }

    protected override void OnDeactivate()
    {
        base.OnDeactivate();
        _gauntletLayer.IsFocusLayer = false;
        ScreenManager.TryLoseFocus(_gauntletLayer);
    }

    protected override void OnFrameTick(float dt)
    {
        base.OnFrameTick(dt);
        if (!_focusSteamLobbyHostSearchOnNextFrame) return;

        _focusSteamLobbyHostSearchOnNextFrame = false;
        FocusSteamLobbyHostSearch();
    }

    protected override void OnFinalize()
    {
        // A Steam lobby search can complete after this screen is popped. Dispose first so the
        // view model rejects that late callback before any bound collections are torn down.
        if (_dataSource != null)
        {
            _dataSource.SteamLobbiesTabActivated -= QueueSteamLobbyHostSearchFocus;
        }
        _dataSource?.Dispose();
        base.OnFinalize();
        RemoveLayer(_gauntletLayer);
        _dataSource = null;
        _gauntletMovie = null;
        _gauntletLayer = null;
    }

    private void QueueSteamLobbyHostSearchFocus()
    {
        _focusSteamLobbyHostSearchOnNextFrame = true;
    }

    private void FocusSteamLobbyHostSearch()
    {
        if (_dataSource?.SelectedTab?.Id != CoopConnectMenuVM.SteamLobbiesTabId) return;

        var eventManager = _gauntletLayer?.UIContext?.EventManager;
        if (eventManager == null || eventManager.IsControllerActive) return;

        var searchInput = _gauntletMovie?.Movie?.RootWidget?
            .FindChild(SteamLobbyHostSearchInputId, includeAllChildren: true) as EditableTextWidget;
        if (searchInput != null)
        {
            eventManager.FocusedWidget = searchInput;
        }
    }
}
