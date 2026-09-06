using TaleWorlds.Engine.GauntletUI;
using System;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.UI.CoopOptions;

public class CoopOptionsUI : ScreenBase
{
    private CoopOptionsVM _dataSource;
    private GauntletLayer _gauntletLayer;
    private GauntletMovieIdentifier _gauntletMovie;

#if DEBUG
    public bool TrySelectDebugTab(string tabId)
    {
        foreach (var tab in _dataSource.Tabs)
        {
            if (tab.Id != tabId) continue;
            tab.ExecuteSelection();
            return true;
        }
        return false;
    }

    public object InspectDebugMenu() => new
    {
        open = true,
        selectedTab = _dataSource.SelectedTab?.Id,
        tabs = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(_dataSource.Tabs, tab => new { tab.Id, tab.Name })),
        appliesChanges = false,
    };
#endif

    protected override void OnInitialize()
    {
        base.OnInitialize();
        if (!ContainerProvider.TryResolve<ICoopOptionsVMFactory>(out var factory))
            throw new InvalidOperationException("Coop options view-model factory is unavailable.");
        _dataSource = factory.Create(ScreenManager.PopScreen);
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
        _dataSource?.OnFinalize();
        _dataSource = null;
        _gauntletLayer = null;
    }
}
