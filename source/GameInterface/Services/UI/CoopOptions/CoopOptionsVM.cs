using Common.Messaging;
using GameInterface;
using GameInterface.Services.UI.CoopOptions.Providers;
using GameInterface.Services.UI.CoopOptions.Providers.KillFeedTab;
using System;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.UI.CoopOptions;

public class CoopOptionsVM : ViewModel
{
    private static readonly ICoopOptionsTabProvider[] TabDefinitions =
    {
        new KillFeedOptionsTabProvider()
    };

    private readonly ICoopOptionsStore optionsStore;
    private readonly IMessageBroker messageBroker;
    private readonly Action close;

    private CoopOptionsTabVM selectedTab;

    public string MovieTextHeader => "Coop Options";
    public string ApplyButtonText => "Apply";

    public CoopOptionsVM() : this(ResolveOptionsStore(), MessageBroker.Instance, ScreenManager.PopScreen)
    {
    }

    public CoopOptionsVM(Action close) : this(ResolveOptionsStore(), MessageBroker.Instance, close)
    {
    }

    public CoopOptionsVM(ICoopOptionsStore optionsStore) : this(optionsStore, MessageBroker.Instance, ScreenManager.PopScreen)
    {
    }

    public CoopOptionsVM(ICoopOptionsStore optionsStore, IMessageBroker messageBroker) :
        this(optionsStore, messageBroker, ScreenManager.PopScreen)
    {
    }

    public CoopOptionsVM(ICoopOptionsStore optionsStore, IMessageBroker messageBroker, Action close)
    {
        this.optionsStore = optionsStore;
        this.messageBroker = messageBroker;
        this.close = close;

        Tabs = new MBBindingList<CoopOptionsTabVM>();
        InitializeTabs(optionsStore.LoadOrDefault());
    }

    [DataSourceProperty]
    public MBBindingList<CoopOptionsTabVM> Tabs { get; }

    [DataSourceProperty]
    public CoopOptionsTabVM SelectedTab
    {
        get => selectedTab;
        private set
        {
            if (selectedTab == value) return;

            selectedTab = value;
            OnPropertyChanged(nameof(SelectedTab));
            OnPropertyChanged(nameof(IsApplyButtonVisible));
        }
    }

    [DataSourceProperty]
    public bool IsApplyButtonVisible => SelectedTab?.CanApply == true;

    public void ActionApply()
    {
        var tab = SelectedTab;
        if (tab == null) return;

        string message = "Coop options successfully updated.";

        try
        {
            var options = optionsStore.LoadOrDefault();
            tab.Apply(options);
            optionsStore.Save(options);
            tab.AfterApply();
        }
        catch
        {
            message = "Coop options unsuccessfully updated.";
        }

        InformationManager.DisplayMessage(new InformationMessage(message));
    }

    public void ActionCancel()
    {
        close();
    }

    private void InitializeTabs(CoopOptionsData options)
    {
        foreach (var provider in TabDefinitions)
        {
            Tabs.Add(provider.CreateTab(options, messageBroker, SelectTab));
        }

        if (Tabs.Count > 0)
        {
            SelectTab(Tabs[0]);
        }
    }

    private void SelectTab(CoopOptionsTabVM tab)
    {
        if (tab == null || SelectedTab == tab) return;

        if (SelectedTab != null)
        {
            SelectedTab.IsSelected = false;
        }

        SelectedTab = tab;
        SelectedTab.IsSelected = true;
    }

    private static ICoopOptionsStore ResolveOptionsStore()
    {
        if (ContainerProvider.TryResolve<ICoopOptionsStore>(out var store))
        {
            return store;
        }

        return new CoopOptionsStore();
    }
}
