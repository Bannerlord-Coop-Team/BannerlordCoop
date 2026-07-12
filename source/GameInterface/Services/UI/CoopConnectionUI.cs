using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.UI;

public class CoopConnectionUI : ScreenBase
{
    private CoopConnectMenuVM _dataSource;
    private GauntletLayer _gauntletLayer;
    private GauntletMovieIdentifier _gauntletMovie;

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
        // A Steam lobby search can complete after this screen is popped. Dispose first so the
        // view model rejects that late callback before any bound collections are torn down.
        _dataSource?.Dispose();
        base.OnFinalize();
        RemoveLayer(_gauntletLayer);
        _dataSource = null;
        _gauntletMovie = null;
        _gauntletLayer = null;
    }
}
