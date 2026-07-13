using Common;
using Common.Messaging;
using Common.Network.Session;
using GameInterface.Services.UI;
using System;
using System.Collections.Generic;
using Xunit;

namespace GameInterface.Tests.Services.UI;

public class CoopConnectMenuVMTests
{
    [Fact]
    public void SearchSteamLobbies_FiltersDisplayedHostNamesCaseInsensitively()
    {
        var browser = new TestSteamLobbyBrowser();
        using var messageBroker = new MessageBroker();
        using var viewModel = new CoopConnectMenuVM(browser, messageBroker);

        SelectSteamLobbiesTab(viewModel);
        browser.Complete(
            CreateLobby(1, "Mountain King"),
            CreateLobby(2, "River Trader"));

        viewModel.SteamLobbyHostSearchText = "tAiN k";
        viewModel.ActionSearchSteamLobbies();

        var match = Assert.Single(viewModel.SteamLobbies);
        Assert.Equal("Mountain King", match.HostText);
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

    private static SteamLobbySummary CreateLobby(ulong lobbyId, string ownerName)
    {
        return new SteamLobbySummary
        {
            LobbyId = lobbyId,
            OwnerName = ownerName,
            ProtocolVersion = SessionJoinInfo.CurrentVersion,
            ModVersion = ModInformation.BuildVersion,
        };
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
