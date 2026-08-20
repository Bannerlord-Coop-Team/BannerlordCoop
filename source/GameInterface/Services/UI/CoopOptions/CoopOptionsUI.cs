using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.UI.CoopOptions;

public class CoopOptionsUI : ScreenBase
{
    private CoopOptionsVM _dataSource;
    private GauntletLayer _gauntletLayer;
    private GauntletMovieIdentifier _gauntletMovie;

#if DEBUG
    private bool _selectPlayerNameplatesTabForDebug;

    internal bool IsPlayerNameplatesTabSelectedForDebug =>
        _dataSource?.PlayerNameplatesTab != null &&
        _dataSource.SelectedTab == _dataSource.PlayerNameplatesTab;

    internal void SelectPlayerNameplatesTabForDebug()
    {
        _selectPlayerNameplatesTabForDebug = true;
    }
#endif

    protected override void OnInitialize()
    {
        base.OnInitialize();
        _dataSource = new CoopOptionsVM();
#if DEBUG
        if (_selectPlayerNameplatesTabForDebug)
        {
            _dataSource.PlayerNameplatesTab?.ExecuteSelection();
        }
#endif
        _gauntletLayer = new GauntletLayer("CoopOptionsUI", 100)
        {
            IsFocusLayer = true
        };
        AddLayer(_gauntletLayer);
        _gauntletLayer.InputRestrictions.SetInputRestrictions();
        _gauntletMovie = _gauntletLayer.LoadMovie("CoopOptionsUIMovie", _dataSource);
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

    protected override void OnFinalize()
    {
        base.OnFinalize();
        RemoveLayer(_gauntletLayer);
        _dataSource = null;
        _gauntletLayer = null;
    }
}
