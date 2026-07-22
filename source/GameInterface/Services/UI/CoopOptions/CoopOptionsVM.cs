using Common;
using Common.Messaging;
using GameInterface;
using GameInterface.Services.UI.CoopOptions.Providers;
using GameInterface.Services.UI.CoopOptions.Providers.KillFeedTab;
using GameInterface.Services.UI.CoopOptions.Providers.ServerOptions;
using GameInterface.Services.UI.Donate;
using System;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace GameInterface.Services.UI.CoopOptions;

public class CoopOptionsVM : ViewModel
{
    private readonly ICoopOptionsStore optionsStore;
    private readonly IMessageBroker messageBroker;
    private readonly Action close;

    private CoopOptionsTabVM selectedTab;

    public string MovieTextHeader => ModInformation.IsServer ? "Server Options" : "Coop Options";
    public string ApplyButtonText => "Apply";
    public string CommunityText => "Join the Community";
    public string DonateButtonText => "Donate";
    public string PatreonButtonText => "Patreon";
    public string DiscordButtonText => "Discord";

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
        if (optionsStore == null) throw new ArgumentNullException(nameof(optionsStore));
        if (messageBroker == null) throw new ArgumentNullException(nameof(messageBroker));
        if (close == null) throw new ArgumentNullException(nameof(close));

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
            OnPropertyChanged(nameof(SelectedKillFeedTab));
            OnPropertyChanged(nameof(SelectedServerOptionsTab));
            OnPropertyChanged(nameof(IsKillFeedOptionsVisible));
            OnPropertyChanged(nameof(IsServerOptionsVisible));
            OnPropertyChanged(nameof(IsApplyButtonVisible));
        }
    }

    [DataSourceProperty]
    public CoopOptionsTabVM SelectedKillFeedTab =>
        SelectedTab?.Id == KillFeedOptionsTabProvider.TabId ? SelectedTab : null;

    [DataSourceProperty]
    public CoopOptionsTabVM SelectedServerOptionsTab =>
        SelectedTab?.Id == ServerOptionsTabProvider.TabId ? SelectedTab : null;

    [DataSourceProperty]
    public bool IsKillFeedOptionsVisible => SelectedKillFeedTab != null;

    [DataSourceProperty]
    public bool IsServerOptionsVisible => SelectedServerOptionsTab != null;

    [DataSourceProperty]
    public bool IsApplyButtonVisible => SelectedTab?.CanApply == true;

    public void ActionApply()
    {
        var tab = SelectedTab;
        if (tab == null) return;

        string message = ModInformation.IsServer
            ? "Server options successfully updated."
            : "Coop options successfully updated.";

        try
        {
            var options = optionsStore.LoadOrDefault();
            tab.Apply(options);
            optionsStore.Save(options);

            tab.AfterApply();
        }
        catch
        {
            message = ModInformation.IsServer
                ? "Server options unsuccessfully updated."
                : "Coop options unsuccessfully updated.";
        }

        InformationManager.DisplayMessage(new InformationMessage(message));
    }

    public void ActionCancel()
    {
        close();
    }

    public void ActionDonate() => CommunityLinks.ShowDonatePopup();

    public void ActionPatreon() => CommunityLinks.OpenPatreon();

    public void ActionDiscord() => CommunityLinks.OpenDiscord();

    private void InitializeTabs(CoopOptionsData options)
    {
        ICoopOptionsTabProvider provider = ModInformation.IsServer
            ? new ServerOptionsTabProvider()
            : new KillFeedOptionsTabProvider();

        Tabs.Add(provider.CreateTab(options, messageBroker, SelectTab));

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
