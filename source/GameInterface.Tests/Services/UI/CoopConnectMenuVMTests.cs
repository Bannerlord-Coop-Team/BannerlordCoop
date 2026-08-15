using Common;
using Common.Messaging;
using Common.Network.Session;
using GameInterface.Services.UI;
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
