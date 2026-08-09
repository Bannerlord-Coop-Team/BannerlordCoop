using Common;
using Common.Messaging;
using Common.Network.Session;
using Common.Network.Session.Messages;
using GameInterface.Services.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GameInterface.Tests.Services.UI;

public class CoopConnectMenuVMTests
{
    [Theory]
    [InlineData("steam", "Steam")]
    [InlineData("gog", "GOG")]
    public void Constructor_ShowsDirectAndOnlyActiveStorefrontBrowser(
        string provider,
        string displayName)
    {
        using var messageBroker = new MessageBroker();
        using var viewModel = new CoopConnectMenuVM(
            new TestSessionBrowser(provider, displayName),
            messageBroker);

        Assert.Equal(2, viewModel.Tabs.Count);
        Assert.Equal("Direct", viewModel.Tabs[0].Name);
        Assert.Equal(displayName + " Lobbies", viewModel.Tabs[1].Name);
        Assert.Equal(CoopConnectMenuVM.SessionBrowserTabId, viewModel.Tabs[1].Id);
    }

    [Fact]
    public void Constructor_WithoutStorefrontBrowserShowsDirectOnly()
    {
        using var messageBroker = new MessageBroker();
        using var viewModel = new CoopConnectMenuVM(
            new TestSessionBrowser(string.Empty, string.Empty, isAvailable: false),
            messageBroker);

        Assert.Single(viewModel.Tabs);
        Assert.Equal(CoopConnectMenuVM.DirectTabId, viewModel.Tabs[0].Id);
    }

    [Fact]
    public void Refresh_FiltersCrossStorefrontCollisionAndJoinsProviderScopedListing()
    {
        var browser = new TestSessionBrowser("gog", "GOG");
        using var messageBroker = new MessageBroker();
        JoinSessionListing requestedJoin = null;
        messageBroker.Subscribe<JoinSessionListing>(payload => requestedJoin = payload.What);
        using var viewModel = new CoopConnectMenuVM(browser, messageBroker);
        SelectSessionBrowserTab(viewModel);

        browser.Complete(
            CreateLobby(42, "GOG Host", provider: "gog"),
            CreateLobby(42, "Steam Host", provider: "steam"));
        Assert.Equal("GOG Host", Assert.Single(viewModel.Sessions).HostText);

        viewModel.Sessions[0].ExecuteJoin();

        Assert.NotNull(requestedJoin);
        Assert.Equal(new SessionListingId("gog", "42"), requestedJoin.ListingId);
    }

    [Fact]
    public void SessionPages_SliceResultsAndStopAtBoundaries()
    {
        var browser = new TestSessionBrowser();
        using var messageBroker = new MessageBroker();
        using var viewModel = new CoopConnectMenuVM(browser, messageBroker);

        SelectSessionBrowserTab(viewModel);
        browser.Complete(Enumerable.Range(1, 9)
            .Select(index => CreateLobby((ulong)index, $"Host {index}"))
            .ToArray());

        Assert.Equal(new[] { "Host 1", "Host 2", "Host 3", "Host 4" }, VisibleHosts(viewModel));
        Assert.Equal("Hosted Steam Servers (9 servers; 0 players)", viewModel.SessionBrowserHeaderText);
        Assert.Equal("Page 1 of 3", viewModel.SessionPageText);
        Assert.True(viewModel.IsSessionPaginationVisible);
        Assert.True(viewModel.IsPreviousSessionPageDisabled);
        Assert.False(viewModel.IsNextSessionPageDisabled);

        viewModel.ActionPreviousSessionPage();
        Assert.Equal("Page 1 of 3", viewModel.SessionPageText);

        viewModel.ActionNextSessionPage();
        Assert.Equal(new[] { "Host 5", "Host 6", "Host 7", "Host 8" }, VisibleHosts(viewModel));
        Assert.Equal("Page 2 of 3", viewModel.SessionPageText);
        Assert.False(viewModel.IsPreviousSessionPageDisabled);
        Assert.False(viewModel.IsNextSessionPageDisabled);

        viewModel.ActionNextSessionPage();
        Assert.Equal(new[] { "Host 9" }, VisibleHosts(viewModel));
        Assert.Equal("Page 3 of 3", viewModel.SessionPageText);
        Assert.True(viewModel.IsNextSessionPageDisabled);

        viewModel.ActionNextSessionPage();
        Assert.Equal("Page 3 of 3", viewModel.SessionPageText);
    }

    [Fact]
    public void SessionSearch_FiltersCompleteCollectionBeforePaginating()
    {
        var browser = new TestSessionBrowser();
        using var messageBroker = new MessageBroker();
        using var viewModel = new CoopConnectMenuVM(browser, messageBroker);

        SelectSessionBrowserTab(viewModel);
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

        viewModel.SessionHostSearchText = "aLpHa";

        Assert.Equal(new[] { "Alpha One", "Alpha Three", "Alpha Five", "Alpha Seven" }, VisibleHosts(viewModel));
        Assert.Equal("Hosted Steam Servers (6 servers; 0 players)", viewModel.SessionBrowserHeaderText);
        Assert.Equal("Page 1 of 2", viewModel.SessionPageText);

        viewModel.ActionNextSessionPage();
        Assert.Equal(new[] { "Alpha Nine", "Alpha Ten" }, VisibleHosts(viewModel));

        viewModel.ActionSearchSessions();
        Assert.Equal("Page 1 of 2", viewModel.SessionPageText);

        viewModel.SessionHostSearchText = "TEN";
        Assert.Equal("Alpha Ten", Assert.Single(viewModel.Sessions).HostText);
        Assert.Equal("Hosted Steam Servers (1 servers; 0 players)", viewModel.SessionBrowserHeaderText);
        Assert.False(viewModel.IsSessionPaginationVisible);

        viewModel.SessionHostSearchText = " ";
        Assert.Equal(new[] { "Alpha One", "Other Two", "Alpha Three", "Other Four" }, VisibleHosts(viewModel));
        Assert.Equal("Hosted Steam Servers (10 servers; 0 players)", viewModel.SessionBrowserHeaderText);
        Assert.Equal("Page 1 of 3", viewModel.SessionPageText);
    }

    [Fact]
    public void SessionRefresh_ResetsAndKeepsPageStateValidWhenResultsShrink()
    {
        var browser = new TestSessionBrowser();
        using var messageBroker = new MessageBroker();
        using var viewModel = new CoopConnectMenuVM(browser, messageBroker);

        SelectSessionBrowserTab(viewModel);
        browser.Complete(Enumerable.Range(1, 9)
            .Select(index => CreateLobby((ulong)index, $"Host {index}"))
            .ToArray());
        viewModel.ActionNextSessionPage();

        viewModel.ActionRefreshSessions();

        Assert.Equal("Hosted Steam Servers (0 servers; 0 players)", viewModel.SessionBrowserHeaderText);
        Assert.Equal("Page 0 of 0", viewModel.SessionPageText);
        Assert.False(viewModel.IsSessionPaginationVisible);

        browser.Complete(CreateLobby(20, "New One"), CreateLobby(21, "New Two"));

        Assert.Equal(new[] { "New One", "New Two" }, VisibleHosts(viewModel));
        Assert.Equal("Hosted Steam Servers (2 servers; 0 players)", viewModel.SessionBrowserHeaderText);
        Assert.Equal("Page 1 of 1", viewModel.SessionPageText);
        Assert.True(viewModel.IsPreviousSessionPageDisabled);
        Assert.True(viewModel.IsNextSessionPageDisabled);
    }

    [Fact]
    public void SearchSessions_FiltersDisplayedHostNamesCaseInsensitively()
    {
        var browser = new TestSessionBrowser();
        using var messageBroker = new MessageBroker();
        using var viewModel = new CoopConnectMenuVM(browser, messageBroker);

        SelectSessionBrowserTab(viewModel);
        browser.Complete(
            CreateLobby(1, "Mountain King", connectedPlayers: 3),
            CreateLobby(2, "River Trader"));

        viewModel.SessionHostSearchText = "tAiN k";
        viewModel.ActionSearchSessions();

        var match = Assert.Single(viewModel.Sessions);
        Assert.Equal("Mountain King", match.HostText);
        Assert.Equal("3", match.ConnectedPlayersText);
        Assert.Equal("Hosted Steam Servers (1 servers; 3 players)", viewModel.SessionBrowserHeaderText);
        Assert.Equal(string.Empty, viewModel.SessionStatusText);
        Assert.Equal(1, browser.RequestCount);
    }

    [Fact]
    public void SearchSessions_UsesDisplayedFallbackAndBlankSearchRestoresAllHosts()
    {
        var browser = new TestSessionBrowser();
        using var messageBroker = new MessageBroker();
        using var viewModel = new CoopConnectMenuVM(browser, messageBroker);

        SelectSessionBrowserTab(viewModel);
        browser.Complete(
            CreateLobby(1, string.Empty),
            CreateLobby(2, "River Trader"));

        Assert.Equal("Hosted Steam Servers (2 servers; 0 players)", viewModel.SessionBrowserHeaderText);

        viewModel.SessionHostSearchText = "UNKNOWN HOST";
        viewModel.ActionSearchSessions();

        Assert.Equal("Unknown host", Assert.Single(viewModel.Sessions).HostText);

        viewModel.SessionHostSearchText = "missing";
        viewModel.ActionSearchSessions();

        Assert.Empty(viewModel.Sessions);
        Assert.Equal("Hosted Steam Servers (0 servers; 0 players)", viewModel.SessionBrowserHeaderText);
        Assert.Equal("No hosts match 'missing'.", viewModel.SessionStatusText);

        viewModel.SessionHostSearchText = "  ";
        viewModel.ActionSearchSessions();

        Assert.Equal(2, viewModel.Sessions.Count);
        Assert.Equal("Hosted Steam Servers (2 servers; 0 players)", viewModel.SessionBrowserHeaderText);
        Assert.Equal(string.Empty, viewModel.SessionStatusText);
    }

    [Fact]
    public void SessionHeader_ShowsServerAndPlayerCountsAndRefreshClearsThem()
    {
        var browser = new TestSessionBrowser();
        using var messageBroker = new MessageBroker();
        using var viewModel = new CoopConnectMenuVM(browser, messageBroker);

        Assert.Equal("Hosted Steam Servers (0 servers; 0 players)", viewModel.SessionBrowserHeaderText);

        SelectSessionBrowserTab(viewModel);
        browser.Complete(
            CreateLobby(1, "Mountain King", connectedPlayers: 3),
            CreateLobby(2, "River Trader", connectedPlayers: 4));
        Assert.Equal("Hosted Steam Servers (2 servers; 7 players)", viewModel.SessionBrowserHeaderText);

        viewModel.ActionRefreshSessions();

        Assert.Equal("Hosted Steam Servers (0 servers; 0 players)", viewModel.SessionBrowserHeaderText);
    }

    [Fact]
    public void SessionSearch_UsesClearHostNameLabelsAndPrompt()
    {
        var browser = new TestSessionBrowser();
        using var messageBroker = new MessageBroker();
        using var viewModel = new CoopConnectMenuVM(browser, messageBroker);

        Assert.Equal("Host Name", viewModel.HostSearchLabelText);
        Assert.Equal("Type a host name...", viewModel.HostSearchPlaceholderText);
        Assert.Equal("Host Name", viewModel.HostColumnText);
        Assert.Equal("Connected Players", viewModel.ConnectedPlayersColumnText);
    }

    [Fact]
    public void SelectingSessionBrowserTab_RequestsSearchFieldFocusOnce()
    {
        var browser = new TestSessionBrowser();
        using var messageBroker = new MessageBroker();
        using var viewModel = new CoopConnectMenuVM(browser, messageBroker);
        int activationCount = 0;
        viewModel.SessionBrowserTabActivated += () => activationCount++;

        SelectSessionBrowserTab(viewModel);
        viewModel.Tabs[1].ExecuteSelection();

        Assert.Equal(1, activationCount);
    }

    private static void SelectSessionBrowserTab(CoopConnectMenuVM viewModel)
    {
        Assert.Equal(CoopConnectMenuVM.SessionBrowserTabId, viewModel.Tabs[1].Id);
        viewModel.Tabs[1].ExecuteSelection();
    }

    private static SessionListing CreateLobby(
        ulong lobbyId,
        string ownerName,
        int connectedPlayers = 0,
        string provider = "steam")
    {
        return new SessionListing
        {
            Id = new SessionListingId(provider, lobbyId.ToString()),
            OwnerName = ownerName,
            ConnectedPlayers = connectedPlayers,
            ProtocolVersion = SessionJoinInfo.CurrentVersion,
            ModVersion = ModInformation.BuildVersion,
        };
    }

    private static string[] VisibleHosts(CoopConnectMenuVM viewModel)
    {
        return viewModel.Sessions.Select(lobby => lobby.HostText).ToArray();
    }

    private sealed class TestSessionBrowser : ISessionBrowser
    {
        private Action<IReadOnlyList<SessionListing>, string>? onCompleted;
        private readonly string provider;
        private readonly string displayName;
        private readonly bool isAvailable;

        public TestSessionBrowser(
            string provider = "steam",
            string displayName = "Steam",
            bool isAvailable = true)
        {
            this.provider = provider;
            this.displayName = displayName;
            this.isAvailable = isAvailable;
        }

        public int RequestCount { get; private set; }
        public string Provider => provider;
        public string DisplayName => displayName;
        public bool IsAvailable => isAvailable;

        public void RequestSessions(Action<IReadOnlyList<SessionListing>, string> callback)
        {
            RequestCount++;
            onCompleted = callback;
        }

        public void Complete(params SessionListing[] lobbies)
        {
            Assert.NotNull(onCompleted);
            onCompleted!(lobbies, string.Empty);
        }
    }
}
