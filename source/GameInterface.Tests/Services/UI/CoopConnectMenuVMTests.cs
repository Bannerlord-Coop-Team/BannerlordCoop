using Common;
using Common.Messaging;
using Common.Network.Session;
using Common.Network.Session.Messages;
using GameInterface.Services.UI;
using GameInterface.Services.UI.CoopOptions;
using GameInterface.Services.UI.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GameInterface.Tests.Services.UI;

public class CoopConnectMenuVMTests
{
    [Fact]
    public void SteamLobbyPages_SliceResultsAndStopAtBoundaries()
    {
        var browser = new TestSteamLobbyBrowser();
        using var messageBroker = new MessageBroker();
        using var viewModel = new CoopConnectMenuVM(browser, messageBroker);

        SelectSteamLobbiesTab(viewModel);
        browser.Complete(Enumerable.Range(1, 9)
            .Select(index => CreateLobby((ulong)index, $"Host {index}"))
            .ToArray());

        Assert.Equal(new[] { "Host 1", "Host 2", "Host 3", "Host 4" }, VisibleHosts(viewModel));
        Assert.Equal("Hosted Steam Servers (9 servers; 0 players)", viewModel.SteamLobbiesHeaderText);
        Assert.Equal("Page 1 of 3", viewModel.SteamLobbyPageText);
        Assert.True(viewModel.IsSteamLobbyPaginationVisible);
        Assert.True(viewModel.IsPreviousSteamLobbyPageDisabled);
        Assert.False(viewModel.IsNextSteamLobbyPageDisabled);

        viewModel.ActionPreviousSteamLobbyPage();
        Assert.Equal("Page 1 of 3", viewModel.SteamLobbyPageText);

        viewModel.ActionNextSteamLobbyPage();
        Assert.Equal(new[] { "Host 5", "Host 6", "Host 7", "Host 8" }, VisibleHosts(viewModel));
        Assert.Equal("Page 2 of 3", viewModel.SteamLobbyPageText);
        Assert.False(viewModel.IsPreviousSteamLobbyPageDisabled);
        Assert.False(viewModel.IsNextSteamLobbyPageDisabled);

        viewModel.ActionNextSteamLobbyPage();
        Assert.Equal(new[] { "Host 9" }, VisibleHosts(viewModel));
        Assert.Equal("Page 3 of 3", viewModel.SteamLobbyPageText);
        Assert.True(viewModel.IsNextSteamLobbyPageDisabled);

        viewModel.ActionNextSteamLobbyPage();
        Assert.Equal("Page 3 of 3", viewModel.SteamLobbyPageText);
    }

    [Fact]
    public void SteamLobbySearch_FiltersCompleteCollectionBeforePaginating()
    {
        var browser = new TestSteamLobbyBrowser();
        using var messageBroker = new MessageBroker();
        using var viewModel = new CoopConnectMenuVM(browser, messageBroker);

        SelectSteamLobbiesTab(viewModel);
        browser.Complete(
            CreateLobby(1, "Alpha One"),
            CreateLobby(2, "Other Two"),
            CreateLobby(3, "Alpha Three"),
            CreateLobby(4, "Other Four"),
            CreateLobby(5, "Alpha Five"),
            CreateLobby(6, "Other Six"),
            CreateLobby(7, "Alpha Seven"),
            CreateLobby(8, "Other Eight"),
            CreateLobby(9, "Alpha Nine"),
            CreateLobby(10, "Alpha Ten"));

        viewModel.SteamLobbyHostSearchText = "aLpHa";

        Assert.Equal(new[] { "Alpha One", "Alpha Three", "Alpha Five", "Alpha Seven" }, VisibleHosts(viewModel));
        Assert.Equal("Hosted Steam Servers (6 servers; 0 players)", viewModel.SteamLobbiesHeaderText);
        Assert.Equal("Page 1 of 2", viewModel.SteamLobbyPageText);

        viewModel.ActionNextSteamLobbyPage();
        Assert.Equal(new[] { "Alpha Nine", "Alpha Ten" }, VisibleHosts(viewModel));

        viewModel.ActionSearchSteamLobbies();
        Assert.Equal("Page 1 of 2", viewModel.SteamLobbyPageText);

        viewModel.SteamLobbyHostSearchText = "TEN";
        Assert.Equal("Alpha Ten", Assert.Single(viewModel.SteamLobbies).HostText);
        Assert.Equal("Hosted Steam Servers (1 servers; 0 players)", viewModel.SteamLobbiesHeaderText);
        Assert.False(viewModel.IsSteamLobbyPaginationVisible);

        viewModel.SteamLobbyHostSearchText = " ";
        Assert.Equal(new[] { "Alpha One", "Other Two", "Alpha Three", "Other Four" }, VisibleHosts(viewModel));
        Assert.Equal("Hosted Steam Servers (10 servers; 0 players)", viewModel.SteamLobbiesHeaderText);
        Assert.Equal("Page 1 of 3", viewModel.SteamLobbyPageText);
    }

    [Fact]
    public void SteamLobbyRefresh_ResetsAndKeepsPageStateValidWhenResultsShrink()
    {
        var browser = new TestSteamLobbyBrowser();
        using var messageBroker = new MessageBroker();
        using var viewModel = new CoopConnectMenuVM(browser, messageBroker);

        SelectSteamLobbiesTab(viewModel);
        browser.Complete(Enumerable.Range(1, 9)
            .Select(index => CreateLobby((ulong)index, $"Host {index}"))
            .ToArray());
        viewModel.ActionNextSteamLobbyPage();

        viewModel.ActionRefreshSteamLobbies();

        Assert.Equal("Hosted Steam Servers (0 servers; 0 players)", viewModel.SteamLobbiesHeaderText);
        Assert.Equal("Page 0 of 0", viewModel.SteamLobbyPageText);
        Assert.False(viewModel.IsSteamLobbyPaginationVisible);

        browser.Complete(CreateLobby(20, "New One"), CreateLobby(21, "New Two"));

        Assert.Equal(new[] { "New One", "New Two" }, VisibleHosts(viewModel));
        Assert.Equal("Hosted Steam Servers (2 servers; 0 players)", viewModel.SteamLobbiesHeaderText);
        Assert.Equal("Page 1 of 1", viewModel.SteamLobbyPageText);
        Assert.True(viewModel.IsPreviousSteamLobbyPageDisabled);
        Assert.True(viewModel.IsNextSteamLobbyPageDisabled);
    }

    [Fact]
    public void SearchSteamLobbies_FiltersDisplayedHostNamesCaseInsensitively()
    {
        var browser = new TestSteamLobbyBrowser();
        using var messageBroker = new MessageBroker();
        using var viewModel = new CoopConnectMenuVM(browser, messageBroker);

        SelectSteamLobbiesTab(viewModel);
        browser.Complete(
            CreateLobby(1, "Mountain King", connectedPlayers: 3),
            CreateLobby(2, "River Trader"));

        viewModel.SteamLobbyHostSearchText = "tAiN k";
        viewModel.ActionSearchSteamLobbies();

        var match = Assert.Single(viewModel.SteamLobbies);
        Assert.Equal("Mountain King", match.HostText);
        Assert.Equal("3", match.ConnectedPlayersText);
        Assert.Equal("Hosted Steam Servers (1 servers; 3 players)", viewModel.SteamLobbiesHeaderText);
        Assert.Equal(string.Empty, viewModel.SteamLobbyStatusText);
        Assert.Equal(1, browser.RequestCount);
    }

    [Fact]
    public void SearchSteamLobbies_UsesDisplayedFallbackAndBlankSearchRestoresAllHosts()
    {
        var browser = new TestSteamLobbyBrowser();
        using var messageBroker = new MessageBroker();
        using var viewModel = new CoopConnectMenuVM(browser, messageBroker);

        SelectSteamLobbiesTab(viewModel);
        browser.Complete(
            CreateLobby(1, string.Empty),
            CreateLobby(2, "River Trader"));

        Assert.Equal("Hosted Steam Servers (2 servers; 0 players)", viewModel.SteamLobbiesHeaderText);

        viewModel.SteamLobbyHostSearchText = "UNKNOWN HOST";
        viewModel.ActionSearchSteamLobbies();

        Assert.Equal("Unknown host", Assert.Single(viewModel.SteamLobbies).HostText);

        viewModel.SteamLobbyHostSearchText = "missing";
        viewModel.ActionSearchSteamLobbies();

        Assert.Empty(viewModel.SteamLobbies);
        Assert.Equal("Hosted Steam Servers (0 servers; 0 players)", viewModel.SteamLobbiesHeaderText);
        Assert.Equal("No hosted Steam lobbies match the current filters.", viewModel.SteamLobbyStatusText);

        viewModel.SteamLobbyHostSearchText = "  ";
        viewModel.ActionSearchSteamLobbies();

        Assert.Equal(2, viewModel.SteamLobbies.Count);
        Assert.Equal("Hosted Steam Servers (2 servers; 0 players)", viewModel.SteamLobbiesHeaderText);
        Assert.Equal(string.Empty, viewModel.SteamLobbyStatusText);
    }

    [Fact]
    public void SteamLobbyHeader_ShowsServerAndPlayerCountsAndRefreshClearsThem()
    {
        var browser = new TestSteamLobbyBrowser();
        using var messageBroker = new MessageBroker();
        using var viewModel = new CoopConnectMenuVM(browser, messageBroker);

        Assert.Equal("Hosted Steam Servers (0 servers; 0 players)", viewModel.SteamLobbiesHeaderText);

        SelectSteamLobbiesTab(viewModel);
        browser.Complete(
            CreateLobby(1, "Mountain King", connectedPlayers: 3),
            CreateLobby(2, "River Trader", connectedPlayers: 4));
        Assert.Equal("Hosted Steam Servers (2 servers; 7 players)", viewModel.SteamLobbiesHeaderText);

        viewModel.ActionRefreshSteamLobbies();

        Assert.Equal("Hosted Steam Servers (0 servers; 0 players)", viewModel.SteamLobbiesHeaderText);
    }

    [Fact]
    public void SteamLobbySearch_UsesClearHostNameLabelsAndPrompt()
    {
        var browser = new TestSteamLobbyBrowser();
        using var messageBroker = new MessageBroker();
        using var viewModel = new CoopConnectMenuVM(browser, messageBroker);

        Assert.Equal("Host Name", viewModel.HostSearchLabelText);
        Assert.Equal("Type a host name...", viewModel.HostSearchPlaceholderText);
        Assert.Equal("Host Name", viewModel.HostColumnText);
        Assert.Equal("Connected Players", viewModel.ConnectedPlayersColumnText);
        Assert.Equal("Password", viewModel.PasswordFilterLabelText);
        Assert.Equal("Minimum Players", viewModel.MinimumPlayersFilterLabelText);
        Assert.Equal("Any Password", viewModel.PasswordFilterButtonText);
        Assert.Equal(0, viewModel.MinimumSteamLobbyPlayers);
    }

    [Fact]
    public void SelectingSteamLobbiesTab_RequestsSearchFieldFocusOnce()
    {
        var browser = new TestSteamLobbyBrowser();
        using var messageBroker = new MessageBroker();
        using var viewModel = new CoopConnectMenuVM(browser, messageBroker);
        int activationCount = 0;
        viewModel.SteamLobbiesTabActivated += () => activationCount++;

        SelectSteamLobbiesTab(viewModel);
        viewModel.Tabs[1].ExecuteSelection();

        Assert.Equal(1, activationCount);
    }
    [Fact]
    public void SteamLobbyPasswordFilter_CyclesThroughAllModes()
    {
        var browser = new TestSteamLobbyBrowser();
        using var messageBroker = new MessageBroker();
        using var viewModel = new CoopConnectMenuVM(browser, messageBroker);
        
        SelectSteamLobbiesTab(viewModel);
        browser.Complete(
            CreateLobby(1, "Open Host", passwordRequired: false),
            CreateLobby(2, "Protected Host", passwordRequired: true));
        
        Assert.Equal(new[] {"Open Host", "Protected Host"}, VisibleHosts(viewModel));
        Assert.Equal("Any Password", viewModel.PasswordFilterButtonText);
        
        viewModel.ActionCycleSteamLobbyPasswordFilter();
        
        Assert.Equal("No Password", viewModel.PasswordFilterButtonText);
        Assert.Equal("Open Host", Assert.Single(viewModel.SteamLobbies).HostText);
        
        viewModel.ActionCycleSteamLobbyPasswordFilter();
        
        Assert.Equal("Password Required", viewModel.PasswordFilterButtonText);
        Assert.Equal("Protected Host", Assert.Single(viewModel.SteamLobbies).HostText);
        
        viewModel.ActionCycleSteamLobbyPasswordFilter();
        
        Assert.Equal("Any Password", viewModel.PasswordFilterButtonText);
        Assert.Equal(2, viewModel.SteamLobbies.Count);
    }
    [Fact]
    public void MinimumSteamLobbyPlayers_FiltersLobbiesAndClampsNegativeValues()
    {
        var browser = new TestSteamLobbyBrowser();
        using var messageBroker = new MessageBroker();
        using var viewModel = new CoopConnectMenuVM(browser, messageBroker);
        
        SelectSteamLobbiesTab(viewModel);
        browser.Complete(
            CreateLobby(1, "Solo Host", connectedPlayers: 1),
            CreateLobby(2, "Busy Host", connectedPlayers: 4),
            CreateLobby(3, "Full Host", connectedPlayers: 8));

        viewModel.MinimumSteamLobbyPlayers = 4;
        
        Assert.Equal(new[] {"Busy Host", "Full Host"}, VisibleHosts(viewModel));
        Assert.Equal("Hosted Steam Servers (2 servers; 12 players)", viewModel.SteamLobbiesHeaderText);
        
        viewModel.MinimumSteamLobbyPlayers = -1;
        
        Assert.Equal(0, viewModel.MinimumSteamLobbyPlayers);
        Assert.Equal(3, viewModel.SteamLobbies.Count);
    }
    [Fact]
    public void SteamLobbyFilters_CombineBeforePagination()
    {
        var browser = new TestSteamLobbyBrowser();
        using var messageBroker = new MessageBroker();
        using var viewModel = new CoopConnectMenuVM(browser, messageBroker);
        
        SelectSteamLobbiesTab(viewModel);
        browser.Complete(
            CreateLobby(1, "Alpha Open", connectedPlayers: 4),
            CreateLobby(2, "Alpha Protected", connectedPlayers: 5, passwordRequired: true),
            CreateLobby(3, "Other Open", connectedPlayers: 6),
            CreateLobby(4, "Alpha Small", connectedPlayers: 1));

        viewModel.SteamLobbyHostSearchText = "Alpha";
        viewModel.MinimumSteamLobbyPlayers = 3;
        viewModel.ActionCycleSteamLobbyPasswordFilter();
        
        Assert.Equal("Alpha Open", Assert.Single(viewModel.SteamLobbies).HostText);
        Assert.Equal("Hosted Steam Servers (1 servers; 4 players)", viewModel.SteamLobbiesHeaderText);
    }

    private static void SelectSteamLobbiesTab(CoopConnectMenuVM viewModel)
    {
        Assert.Equal(CoopConnectMenuVM.SteamLobbiesTabId, viewModel.Tabs[1].Id);
        viewModel.Tabs[1].ExecuteSelection();
    }

    private static SteamLobbySummary CreateLobby(ulong lobbyId, string ownerName, int connectedPlayers = 0, bool passwordRequired = false)
    {
        return new SteamLobbySummary
        {
            LobbyId = lobbyId,
            OwnerName = ownerName,
            ConnectedPlayers = connectedPlayers,
            PasswordRequired = passwordRequired,
            ProtocolVersion = SessionJoinInfo.CurrentVersion,
            ModVersion = ModInformation.BuildVersion,
        };
    }

    private static string[] VisibleHosts(CoopConnectMenuVM viewModel)
    {
        return viewModel.SteamLobbies.Select(lobby => lobby.HostText).ToArray();
    }

    [Fact]
    public void LastConnectionTab_IsVisible_WhenSelected()
    {
        var browser = new TestSteamLobbyBrowser();
        using var messageBroker = new MessageBroker();
        using var viewModel = new CoopConnectMenuVM(browser, messageBroker);

        Assert.False(viewModel.IsLastConnectionTabVisible);

        SelectLastConnectionTab(viewModel);

        Assert.True(viewModel.IsLastConnectionTabVisible);
        Assert.False(viewModel.IsDirectTabVisible);
        Assert.False(viewModel.IsSteamLobbiesTabVisible);
    }

    [Fact]
    public void LastConnection_HasNoLastConnection_Initially()
    {
        var browser = new TestSteamLobbyBrowser();
        using var messageBroker = new MessageBroker();
        var store = new TestCoopOptionsStore();
        using var viewModel = new CoopConnectMenuVM(browser, messageBroker, store);

        Assert.True(viewModel.HasNoLastConnection);
        Assert.False(viewModel.HasLastDirectConnection);
        Assert.False(viewModel.HasLastSteamLobby);
    }

    [Fact]
    public void LastConnection_DirectConnection_SavedAfterConnect()
    {
        var browser = new TestSteamLobbyBrowser();
        using var messageBroker = new MessageBroker();
        var store = new TestCoopOptionsStore();
        using var viewModel = new CoopConnectMenuVM(browser, messageBroker, store);

        viewModel.Ip = "127.0.0.1";
        viewModel.Port = "4200";
        viewModel.Password = "secret";
        viewModel.ActionConnect();
        messageBroker.Publish(viewModel, new ClientNetworkConnected());

        Assert.True(viewModel.HasLastDirectConnection);
        Assert.False(viewModel.HasNoLastConnection);
        Assert.DoesNotContain("127.0.0.1", viewModel.LastDirectConnectionText);

        var saved = store.Options.GetSectionOrDefault<LastConnectionData>(
            LastConnectionData.TabId, LastConnectionData.SectionId, null);
        Assert.NotNull(saved);
        Assert.Equal("127.0.0.1", saved.DirectIp);
        Assert.Equal("4200", saved.DirectPort);
        Assert.NotNull(saved.DirectPasswordProtected);
    }

    [Fact]
    public void LastConnection_DirectConnection_LoadedByNewInstance()
    {
        var browser = new TestSteamLobbyBrowser();
        using var messageBroker = new MessageBroker();
        var store = new TestCoopOptionsStore();

        using (var viewModel = new CoopConnectMenuVM(browser, messageBroker, store))
        {
            viewModel.Ip = "10.0.0.1";
            viewModel.Port = "5000";
            viewModel.Password = "";
            viewModel.ActionConnect();
            messageBroker.Publish(viewModel, new ClientNetworkConnected());
        }

        using var viewModel2 = new CoopConnectMenuVM(browser, messageBroker, store);
        Assert.True(viewModel2.HasLastDirectConnection);
        Assert.Equal("10.0.0.1", viewModel2.Ip);
        Assert.Equal("5000", viewModel2.Port);
        Assert.DoesNotContain("10.0.0.1", viewModel2.LastDirectConnectionText);
    }

    [Fact]
    public void LastConnection_SteamLobby_SavedAfterJoin()
    {
        var browser = new TestSteamLobbyBrowser();
        using var messageBroker = new MessageBroker();
        var store = new TestCoopOptionsStore();
        using var viewModel = new CoopConnectMenuVM(browser, messageBroker, store);

        SelectSteamLobbiesTab(viewModel);
        browser.Complete(CreateLobby(999, "Castle Keep"));
        viewModel.SteamLobbies[0].ExecuteJoin();
        messageBroker.Publish(viewModel, new ClientNetworkConnected());

        Assert.True(viewModel.HasLastSteamLobby);
        Assert.False(viewModel.HasNoLastConnection);
        Assert.DoesNotContain("Castle Keep", viewModel.LastSteamLobbyText);

        var saved = store.Options.GetSectionOrDefault<LastConnectionData>(
            LastConnectionData.TabId, LastConnectionData.SectionId, null);
        Assert.NotNull(saved);
        Assert.Equal(999UL, saved.SteamLobbyId);
        Assert.Equal("Castle Keep", saved.SteamLobbyHostName);
    }

    [Fact]
    public void LastConnection_SteamLobby_LoadedByNewInstance()
    {
        var browser = new TestSteamLobbyBrowser();
        using var messageBroker = new MessageBroker();
        var store = new TestCoopOptionsStore();

        using (var viewModel = new CoopConnectMenuVM(browser, messageBroker, store))
        {
            SelectSteamLobbiesTab(viewModel);
            browser.Complete(CreateLobby(42, "Iron Citadel"));
            viewModel.SteamLobbies[0].ExecuteJoin();
            messageBroker.Publish(viewModel, new ClientNetworkConnected());
        }

        using var viewModel2 = new CoopConnectMenuVM(browser, messageBroker, store);
        Assert.True(viewModel2.HasLastSteamLobby);
        Assert.DoesNotContain("Iron Citadel", viewModel2.LastSteamLobbyText);
    }

    [Fact]
    public void LastConnection_HasNoLastConnection_RefreshesAfterConnect()
    {
        var browser = new TestSteamLobbyBrowser();
        using var messageBroker = new MessageBroker();
        var store = new TestCoopOptionsStore();
        using var viewModel = new CoopConnectMenuVM(browser, messageBroker, store);

        Assert.True(viewModel.HasNoLastConnection);

        viewModel.Ip = "127.0.0.1";
        viewModel.Port = "4200";
        viewModel.ActionConnect();
        messageBroker.Publish(viewModel, new ClientNetworkConnected());

        Assert.False(viewModel.HasNoLastConnection);
        Assert.True(viewModel.HasLastDirectConnection);
    }

    [Fact]
    public void LastConnection_ActionReconnectDirect_UsesSnapshot()
    {
        var browser = new TestSteamLobbyBrowser();
        using var messageBroker = new MessageBroker();
        var store = new TestCoopOptionsStore();
        using var viewModel = new CoopConnectMenuVM(browser, messageBroker, store);

        viewModel.Ip = "10.0.0.1";
        viewModel.Port = "4200";
        viewModel.ActionConnect();
        messageBroker.Publish(viewModel, new ClientNetworkConnected());

        // Edit the live form to a different address — reconnect must ignore this
        viewModel.Ip = "1.2.3.4";

        AttemptJoin published = null;
        messageBroker.Subscribe<AttemptJoin>(payload => published = payload.What);
        viewModel.ActionReconnectDirect();

        Assert.NotNull(published);
        Assert.Equal("10.0.0.1", published.Address.ToString());
    }

    [Fact]
    public void LastConnection_ActionReconnectSteamLobby_UsesSnapshot()
    {
        var browser = new TestSteamLobbyBrowser();
        using var messageBroker = new MessageBroker();
        var store = new TestCoopOptionsStore();
        using var viewModel = new CoopConnectMenuVM(browser, messageBroker, store);

        SelectSteamLobbiesTab(viewModel);
        browser.Complete(CreateLobby(777, "Test Keep"));
        viewModel.SteamLobbies[0].ExecuteJoin();
        messageBroker.Publish(viewModel, new ClientNetworkConnected());

        JoinSteamLobby published = null;
        messageBroker.Subscribe<JoinSteamLobby>(payload => published = payload.What);
        viewModel.ActionReconnectSteamLobby();

        Assert.NotNull(published);
        Assert.Equal(777UL, published.LobbyId);
        Assert.Null(published.PreSuppliedPassword);
    }

    [Fact]
    public void LastConnection_SteamLobby_DirectPasswordNotSavedOnJoin()
    {
        var browser = new TestSteamLobbyBrowser();
        using var messageBroker = new MessageBroker();
        var store = new TestCoopOptionsStore();

        using (var viewModel = new CoopConnectMenuVM(browser, messageBroker, store))
        {
            SelectSteamLobbiesTab(viewModel);
            viewModel.Password = "directformpassword";
            browser.Complete(CreateLobby(42, "Fortress"));
            viewModel.SteamLobbies[0].ExecuteJoin();
            messageBroker.Publish(viewModel, new ClientNetworkConnected());
        }

        var saved = store.Options.GetSectionOrDefault<LastConnectionData>(
            LastConnectionData.TabId, LastConnectionData.SectionId, null);
        Assert.True(string.IsNullOrEmpty(saved.SteamLobbyPasswordProtected));

        using var viewModel2 = new CoopConnectMenuVM(browser, messageBroker, store);
        JoinSteamLobby published = null;
        messageBroker.Subscribe<JoinSteamLobby>(payload => published = payload.What);
        viewModel2.ActionReconnectSteamLobby();

        Assert.NotNull(published);
        Assert.True(string.IsNullOrEmpty(published.PreSuppliedPassword));
    }

    [Fact]
    public void LastConnection_NoSave_IfNetworkConnectedNeverFires()
    {
        var browser = new TestSteamLobbyBrowser();
        using var messageBroker = new MessageBroker();
        var store = new TestCoopOptionsStore();
        using var viewModel = new CoopConnectMenuVM(browser, messageBroker, store);

        viewModel.Ip = "10.0.0.1";
        viewModel.Port = "4200";
        viewModel.ActionConnect();
        // ClientNetworkConnected deliberately NOT published — connection failed

        var saved = store.Options.GetSectionOrDefault<LastConnectionData>(
            LastConnectionData.TabId, LastConnectionData.SectionId, null);
        Assert.Null(saved);
        Assert.True(viewModel.HasNoLastConnection);
    }

    [Fact]
    public void LastConnection_HasNoLastConnection_RefreshesAfterSteamLobbyJoin()
    {
        var browser = new TestSteamLobbyBrowser();
        using var messageBroker = new MessageBroker();
        var store = new TestCoopOptionsStore();
        using var viewModel = new CoopConnectMenuVM(browser, messageBroker, store);

        Assert.True(viewModel.HasNoLastConnection);

        SelectSteamLobbiesTab(viewModel);
        browser.Complete(CreateLobby(1, "Arena"));
        viewModel.SteamLobbies[0].ExecuteJoin();
        messageBroker.Publish(viewModel, new ClientNetworkConnected());

        Assert.False(viewModel.HasNoLastConnection);
        Assert.True(viewModel.HasLastSteamLobby);
    }

    [Fact]
    public void LastConnection_DirectConnection_NotPersistedInMemory_OnSaveFailure()
    {
        var browser = new TestSteamLobbyBrowser();
        using var messageBroker = new MessageBroker();
        var store = new FailingCoopOptionsStore();
        using var viewModel = new CoopConnectMenuVM(browser, messageBroker, store);

        viewModel.Ip = "10.0.0.1";
        viewModel.Port = "4200";
        viewModel.ActionConnect();
        messageBroker.Publish(viewModel, new ClientNetworkConnected());

        Assert.True(viewModel.HasNoLastConnection);
        Assert.False(viewModel.HasLastDirectConnection);
    }

    [Fact]
    public void LastConnection_SteamLobby_NotPersistedInMemory_OnSaveFailure()
    {
        var browser = new TestSteamLobbyBrowser();
        using var messageBroker = new MessageBroker();
        var store = new FailingCoopOptionsStore();
        using var viewModel = new CoopConnectMenuVM(browser, messageBroker, store);

        SelectSteamLobbiesTab(viewModel);
        browser.Complete(CreateLobby(42, "Test Keep"));
        viewModel.SteamLobbies[0].ExecuteJoin();
        messageBroker.Publish(viewModel, new ClientNetworkConnected());

        Assert.True(viewModel.HasNoLastConnection);
        Assert.False(viewModel.HasLastSteamLobby);
    }

    [Fact]
    public void NormalSteamJoin_DoesNotPassDirectConnectPassword_AsPreSuppliedPassword()
    {
        var browser = new TestSteamLobbyBrowser();
        using var messageBroker = new MessageBroker();
        using var viewModel = new CoopConnectMenuVM(browser, messageBroker);

        viewModel.Password = "directpass";

        JoinSteamLobby published = null;
        messageBroker.Subscribe<JoinSteamLobby>(payload => published = payload.What);

        SelectSteamLobbiesTab(viewModel);
        browser.Complete(CreateLobby(42, "Test Keep"));
        viewModel.SteamLobbies[0].ExecuteJoin();

        Assert.NotNull(published);
        Assert.Null(published.PreSuppliedPassword);
    }

    [Fact]
    public void FailedDirect_ThenSteamSuccess_PersistsSteamNotDirect()
    {
        var browser = new TestSteamLobbyBrowser();
        using var messageBroker = new MessageBroker();
        var store = new TestCoopOptionsStore();
        using var viewModel = new CoopConnectMenuVM(browser, messageBroker, store);

        // Direct attempt — connection never confirmed (no ClientNetworkConnected)
        viewModel.Ip = "10.0.0.1";
        viewModel.Port = "4200";
        viewModel.ActionConnect();

        // Steam join now succeeds
        SelectSteamLobbiesTab(viewModel);
        browser.Complete(CreateLobby(999, "Stronghold"));
        viewModel.SteamLobbies[0].ExecuteJoin();
        messageBroker.Publish(viewModel, new ClientNetworkConnected());

        Assert.False(viewModel.HasLastDirectConnection);
        Assert.True(viewModel.HasLastSteamLobby);
    }

    private static void SelectLastConnectionTab(CoopConnectMenuVM viewModel)
    {
        Assert.Equal(CoopConnectMenuVM.LastConnectionTabId, viewModel.Tabs[2].Id);
        viewModel.Tabs[2].ExecuteSelection();
    }

    private sealed class FailingCoopOptionsStore : ICoopOptionsStore
    {
        public string FilePath => null;

        public bool TryLoad(out CoopOptionsData options) { options = new CoopOptionsData(); return true; }

        public CoopOptionsData LoadOrDefault() => new CoopOptionsData();

        public void Save(CoopOptionsData options) => throw new System.IO.IOException("disk full");
    }

    private sealed class TestCoopOptionsStore : ICoopOptionsStore
    {
        public CoopOptionsData Options { get; private set; } = new CoopOptionsData();

        public string FilePath => null;

        public bool TryLoad(out CoopOptionsData options)
        {
            options = Options;
            return true;
        }

        public CoopOptionsData LoadOrDefault() => Options;

        public void Save(CoopOptionsData options) => Options = options;
    }

    private sealed class TestSteamLobbyBrowser : ISteamLobbyBrowser
    {
        private Action<IReadOnlyList<SteamLobbySummary>, string>? onCompleted;

        public int RequestCount { get; private set; }

        public void RequestLobbies(Action<IReadOnlyList<SteamLobbySummary>, string> callback)
        {
            RequestCount++;
            onCompleted = callback;
        }

        public void Complete(params SteamLobbySummary[] lobbies)
        {
            Assert.NotNull(onCompleted);
            onCompleted!(lobbies, string.Empty);
        }
    }
}
