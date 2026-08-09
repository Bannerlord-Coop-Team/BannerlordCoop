using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.UI;

public class CoopConnectionUI : ScreenBase
{
    private const string SessionHostSearchInputId = "SessionHostSearchInput";

    private CoopConnectMenuVM _dataSource;
    private GauntletLayer _gauntletLayer;
    private GauntletMovieIdentifier _gauntletMovie;
    private bool _focusSessionHostSearchOnNextFrame;

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
        _dataSource.SessionBrowserTabActivated += QueueSessionHostSearchFocus;
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
        if (!_focusSessionHostSearchOnNextFrame) return;

        _focusSessionHostSearchOnNextFrame = false;
        FocusSessionHostSearch();
    }

    protected override void OnFinalize()
    {
        // A provider search can complete after this screen is popped. Dispose first so the
        // view model rejects that late callback before any bound collections are torn down.
        if (_dataSource != null)
        {
            _dataSource.SessionBrowserTabActivated -= QueueSessionHostSearchFocus;
        }
        _dataSource?.Dispose();
        base.OnFinalize();
        RemoveLayer(_gauntletLayer);
        _dataSource = null;
        _gauntletMovie = null;
        _gauntletLayer = null;
    }

    private void QueueSessionHostSearchFocus()
    {
        _focusSessionHostSearchOnNextFrame = true;
    }

    private void FocusSessionHostSearch()
    {
        if (_dataSource?.SelectedTab?.Id != CoopConnectMenuVM.SessionBrowserTabId) return;

        var eventManager = _gauntletLayer?.UIContext?.EventManager;
        if (eventManager == null || eventManager.IsControllerActive) return;

        var searchInput = _gauntletMovie?.Movie?.RootWidget?
            .FindChild(SessionHostSearchInputId, includeAllChildren: true) as EditableTextWidget;
        if (searchInput != null)
        {
            eventManager.FocusedWidget = searchInput;
        }
    }
}
