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
        Assert.Equal("Hosted Steam Servers (9)", viewModel.SteamLobbiesHeaderText);
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
        Assert.Equal("Hosted Steam Servers (6)", viewModel.SteamLobbiesHeaderText);
        Assert.Equal("Page 1 of 2", viewModel.SteamLobbyPageText);

        viewModel.ActionNextSteamLobbyPage();
        Assert.Equal(new[] { "Alpha Nine", "Alpha Ten" }, VisibleHosts(viewModel));

        viewModel.ActionSearchSteamLobbies();
        Assert.Equal("Page 1 of 2", viewModel.SteamLobbyPageText);

        viewModel.SteamLobbyHostSearchText = "TEN";
        Assert.Equal("Alpha Ten", Assert.Single(viewModel.SteamLobbies).HostText);
        Assert.Equal("Hosted Steam Servers (1)", viewModel.SteamLobbiesHeaderText);
        Assert.False(viewModel.IsSteamLobbyPaginationVisible);

        viewModel.SteamLobbyHostSearchText = " ";
        Assert.Equal(new[] { "Alpha One", "Other Two", "Alpha Three", "Other Four" }, VisibleHosts(viewModel));
        Assert.Equal("Hosted Steam Servers (10)", viewModel.SteamLobbiesHeaderText);
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

        Assert.Equal("Hosted Steam Servers (0)", viewModel.SteamLobbiesHeaderText);
        Assert.Equal("Page 0 of 0", viewModel.SteamLobbyPageText);
        Assert.False(viewModel.IsSteamLobbyPaginationVisible);

        browser.Complete(CreateLobby(20, "New One"), CreateLobby(21, "New Two"));

        Assert.Equal(new[] { "New One", "New Two" }, VisibleHosts(viewModel));
        Assert.Equal("Hosted Steam Servers (2)", viewModel.SteamLobbiesHeaderText);
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
        Assert.Equal("Hosted Steam Servers (1)", viewModel.SteamLobbiesHeaderText);
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

        Assert.Equal("Hosted Steam Servers (2)", viewModel.SteamLobbiesHeaderText);

        viewModel.SteamLobbyHostSearchText = "UNKNOWN HOST";
        viewModel.ActionSearchSteamLobbies();

        Assert.Equal("Unknown host", Assert.Single(viewModel.SteamLobbies).HostText);

        viewModel.SteamLobbyHostSearchText = "missing";
        viewModel.ActionSearchSteamLobbies();

        Assert.Empty(viewModel.SteamLobbies);
        Assert.Equal("Hosted Steam Servers (0)", viewModel.SteamLobbiesHeaderText);
        Assert.Equal("No hosts match 'missing'.", viewModel.SteamLobbyStatusText);

        viewModel.SteamLobbyHostSearchText = "  ";
        viewModel.ActionSearchSteamLobbies();

        Assert.Equal(2, viewModel.SteamLobbies.Count);
        Assert.Equal("Hosted Steam Servers (2)", viewModel.SteamLobbiesHeaderText);
        Assert.Equal(string.Empty, viewModel.SteamLobbyStatusText);
    }

    [Fact]
    public void SteamLobbyHeader_StartsAtZeroAndRefreshClearsTheCount()
    {
        var browser = new TestSteamLobbyBrowser();
        using var messageBroker = new MessageBroker();
        using var viewModel = new CoopConnectMenuVM(browser, messageBroker);

        Assert.Equal("Hosted Steam Servers (0)", viewModel.SteamLobbiesHeaderText);

        SelectSteamLobbiesTab(viewModel);
        browser.Complete(CreateLobby(1, "Mountain King"));
        Assert.Equal("Hosted Steam Servers (1)", viewModel.SteamLobbiesHeaderText);

        viewModel.ActionRefreshSteamLobbies();

        Assert.Equal("Hosted Steam Servers (0)", viewModel.SteamLobbiesHeaderText);
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

    private static void SelectSteamLobbiesTab(CoopConnectMenuVM viewModel)
    {
        Assert.Equal(CoopConnectMenuVM.SteamLobbiesTabId, viewModel.Tabs[1].Id);
        viewModel.Tabs[1].ExecuteSelection();
    }

    private static SteamLobbySummary CreateLobby(ulong lobbyId, string ownerName, int connectedPlayers = 0)
    {
        return new SteamLobbySummary
        {
            LobbyId = lobbyId,
            OwnerName = ownerName,
            ConnectedPlayers = connectedPlayers,
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
