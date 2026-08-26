using Common.Messaging;
using GameInterface.Configuration;
using GameInterface.Services.CampaignService.Messages;
using GameInterface.Services.UI.CoopOptions.Providers;
using GameInterface.Services.UI.CoopOptions.Providers.ChatTab;
using GameInterface.Services.UI.CoopOptions.Providers.KillFeedTab;
using GameInterface.Services.UI.CoopOptions.Providers.MapTimeTab;
using GameInterface.Services.UI.CoopOptions.Providers.NetworkTab;
using GameInterface.Services.UI.CoopOptions.Providers.PlayerNameplatesTab;
using GameInterface.Services.UI.Donate;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Library;

namespace GameInterface.Services.UI.CoopOptions;

public class CoopOptionsVM : ViewModel
{
    private readonly ICoopOptionsStore optionsStore;
    private readonly IMessageBroker messageBroker;
    private readonly IReadOnlyList<ICoopOptionsTabProvider> tabProviders;
    private readonly Action close;

    private ModOptions modOptions;
    private CoopOptionsTabVM selectedTab;

    public string MovieTextHeader => "Coop Options";
    public string ApplyButtonText => "Apply";
    public string CommunityText => "Join the Community";
    public string CreditsButtonText => "Credits";
    public string DonateButtonText => "Donate";
    public string PatreonButtonText => "Patreon";
    public string DiscordButtonText => "Discord";

    public CoopOptionsVM(
        ICoopOptionsStore optionsStore,
        IMessageBroker messageBroker,
        IEnumerable<ICoopOptionsTabProvider> tabProviders,
        ModOptions modOptions,
        Action close)
    {
        if (optionsStore == null) throw new ArgumentNullException(nameof(optionsStore));
        if (messageBroker == null) throw new ArgumentNullException(nameof(messageBroker));
        if (tabProviders == null) throw new ArgumentNullException(nameof(tabProviders));
        if (close == null) throw new ArgumentNullException(nameof(close));

        this.optionsStore = optionsStore;
        this.messageBroker = messageBroker;
        this.tabProviders = tabProviders.ToArray();
        this.modOptions = modOptions;
        this.close = close;

        Tabs = new MBBindingList<CoopOptionsTabVM>();
        messageBroker.Subscribe<ModConfigApplied>(HandleModConfigApplied);
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

    [DataSourceProperty]
    public CoopOptionsTabVM KillFeedTab { get; set; }

    [DataSourceProperty]
    public CoopOptionsTabVM MapTimeTab { get; set; }

    [DataSourceProperty]
    public CoopOptionsTabVM ChatTab { get; set; }

    [DataSourceProperty]
    public CoopOptionsTabVM PlayerNameplatesTab { get; set; }

    [DataSourceProperty]
    public CoopOptionsTabVM NetworkTab { get; set; }

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

    public void ActionDonate() => CommunityLinks.ShowDonatePopup();

    public void ActionCredits() => CommunityLinks.ShowCreditsPopup();

    public void ActionPatreon() => CommunityLinks.OpenPatreon();

    public void ActionDiscord() => CommunityLinks.OpenDiscord();

    public override void OnFinalize()
    {
        messageBroker.Unsubscribe<ModConfigApplied>(HandleModConfigApplied);
        foreach (var tab in Tabs)
            tab.OnFinalize();
        base.OnFinalize();
    }

    private void InitializeTabs(CoopOptionsData options)
    {
        SynchronizeTabs(options);
        if (Tabs.Count > 0)
            SelectTab(Tabs[0]);
    }

    private void HandleModConfigApplied(MessagePayload<ModConfigApplied> payload)
    {
        modOptions = payload.What.ModOptions;
        SynchronizeTabs(optionsStore.LoadOrDefault());
    }

    private void SynchronizeTabs(CoopOptionsData options)
    {
        foreach (var provider in tabProviders)
        {
            var existingTab = Tabs.FirstOrDefault(tab => tab.Id == provider.Id);
            if (!provider.IsAvailable(modOptions))
            {
                if (existingTab != null) RemoveTab(existingTab);
                continue;
            }

            if (existingTab != null) continue;

            var tab = provider.CreateTab(options, messageBroker, SelectTab);
            Tabs.Add(tab);
            SetTabReference(tab.Id, tab);
        }

        if (SelectedTab == null && Tabs.Count > 0)
            SelectTab(Tabs[0]);
    }

    private void RemoveTab(CoopOptionsTabVM tab)
    {
        if (SelectedTab == tab)
        {
            tab.IsSelected = false;
            SelectedTab = null;
        }

        Tabs.Remove(tab);
        SetTabReference(tab.Id, null);
        tab.OnFinalize();
    }

    private void SetTabReference(string tabId, CoopOptionsTabVM tab)
    {
        if (tabId == KillFeedOptionsTabProvider.TabId)
        {
            KillFeedTab = tab;
            OnPropertyChanged(nameof(KillFeedTab));
        }
        else if (tabId == MapTimeOptionsTabProvider.TabId)
        {
            MapTimeTab = tab;
            OnPropertyChanged(nameof(MapTimeTab));
        }
        else if (tabId == ChatOptionsTabProvider.TabId)
        {
            ChatTab = tab;
            OnPropertyChanged(nameof(ChatTab));
        }
        else if (tabId == PlayerNameplatesOptionsTabProvider.TabId)
        {
            PlayerNameplatesTab = tab;
            OnPropertyChanged(nameof(PlayerNameplatesTab));
        }
        else if (tabId == NetworkOptionsTabProvider.TabId)
        {
            NetworkTab = tab;
            OnPropertyChanged(nameof(NetworkTab));
        }
    }

    private void SelectTab(CoopOptionsTabVM tab)
    {
        if (tab == null || SelectedTab == tab) return;

        if (SelectedTab != null)
            SelectedTab.IsSelected = false;

        SelectedTab = tab;
        SelectedTab.IsSelected = true;
    }
}
