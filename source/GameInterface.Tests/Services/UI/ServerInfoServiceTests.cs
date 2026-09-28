using GameInterface.Services.UI.ServerInfo;
using System;
using System.Linq;
using Xunit;
using static GameInterface.Tests.Services.UI.ServerInfoVMTests;

namespace GameInterface.Tests.Services.UI;

/// <summary>Protects when the server info panel opens: once per join, after the map is free, and never empty.</summary>
[Collection(ViewModelCollection.Name)]
public class ServerInfoServiceTests
{
    private static readonly NetworkServerInfo Welcome = new(new[] { "Welcome to EU-1", "Restart 06:00 UTC" }, null, null, null);

    // Info that arrives before the campaign map or while something else has focus waits for it.
    [Fact]
    public void InfoWaitsUntilTheMapIsAvailable()
    {
        var popup = new FakePopup { CanOpenResult = false };
        using var service = popup.CreateService();

        service.Show(Welcome);
        service.Update();
        Assert.Null(popup.ViewModel);
        Assert.Contains("Pending: True", service.Describe());

        service.Initialize();
        service.Update();
        Assert.False(popup.ViewModel!.IsOpen);
        Assert.Contains("Pending: True", service.Describe());

        popup.CanOpenResult = true;
        service.Update();
        Assert.True(popup.ViewModel.IsOpen);
        Assert.Equal(Welcome.Motd, popup.ViewModel.Paragraphs.Select(paragraph => paragraph.Text));
        Assert.Equal(1, popup.Opened);
        Assert.Contains("Pending: False", service.Describe());
    }

    // Closing by Escape, the button or leaving the map never reopens the same info.
    [Fact]
    public void ClosedPanelStaysClosed()
    {
        var popup = new FakePopup();
        using var service = popup.CreateService();
        service.Initialize();
        service.Show(Welcome);
        service.Update();

        popup.ViewModel!.ExecuteClose();
        service.Update();
        service.Update();

        Assert.False(popup.ViewModel.IsOpen);
        Assert.Equal(1, popup.Opened);
        Assert.Equal(1, popup.Closed);
        Assert.Contains("Pending: False", service.Describe());
    }

    // Each successful join sends the info again, so a rejoin shows it again.
    [Fact]
    public void NewJoinShowsTheInfoAgain()
    {
        var popup = new FakePopup();
        using var service = popup.CreateService();
        service.Initialize();
        service.Show(Welcome);
        service.Update();
        popup.ViewModel!.ExecuteClose();

        service.Show(new NetworkServerInfo(new[] { "Welcome back" }, null, null, null));
        service.Update();

        Assert.True(popup.ViewModel.IsOpen);
        Assert.Equal(2, popup.Opened);
        Assert.Equal("Welcome back", Assert.Single(popup.ViewModel.Paragraphs).Text);
    }

    // Two infos before the map is free open once with the newest; one while open replaces it in place.
    [Fact]
    public void NewestInfoWinsWithoutASecondOpen()
    {
        var popup = new FakePopup { CanOpenResult = false };
        using var service = popup.CreateService();
        service.Initialize();
        service.Show(new NetworkServerInfo(new[] { "First" }, null, null, null));
        service.Show(new NetworkServerInfo(new[] { "Second" }, null, null, null));
        popup.CanOpenResult = true;
        service.Update();
        service.Show(new NetworkServerInfo(new[] { "Third" }, null, null, null));
        service.Update();

        Assert.Equal(1, popup.Opened);
        Assert.Equal("Third", Assert.Single(popup.ViewModel!.Paragraphs).Text);
        Assert.Contains("Pending: False", service.Describe());
    }

    // Every open starts on the first tab, whichever tab was left selected.
    [Fact]
    public void EachOpenStartsOnTheFirstTab()
    {
        var popup = new FakePopup();
        using var service = popup.CreateService();
        service.Initialize();
        service.Show(Info(true, true, true, true));
        service.Update();
        popup.ViewModel!.ExecuteSelectTab((int)ServerInfoTab.News);
        popup.ViewModel.ExecuteClose();

        service.Show(Info(true, true, true, true));
        service.Update();

        Assert.Equal(ServerInfoTab.Motd, popup.ViewModel.SelectedTab);

        service.Show(Info(false, true, true, true));
        popup.ViewModel.ExecuteClose();
        service.Show(Info(false, false, true, true));
        service.Update();

        Assert.Equal(ServerInfoTab.Links, popup.ViewModel.SelectedTab);
    }

    // No info, an empty one or one whose links the client refuses never open an empty panel.
    [Theory]
    [MemberData(nameof(EmptyInfos))]
    public void InfoWithNothingToShow_OpensNothing(NetworkServerInfo info)
    {
        var popup = new FakePopup();
        using var service = popup.CreateService();
        service.Initialize();

        service.Show(info);
        service.Update();

        Assert.False(popup.ViewModel!.IsOpen);
        Assert.Equal(0, popup.Opened);
        Assert.StartsWith("Open: False\nPending: False\nTab: none\nTabs: none\nCounts: motd 0, rules 0, links 0, news 0", service.Describe());
    }

    public static TheoryData<NetworkServerInfo> EmptyInfos => new()
    {
        null!,
        new NetworkServerInfo(null, null, null, null),
        new NetworkServerInfo(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<ServerInfoLink>(), Array.Empty<ServerInfoNews>()),
        new NetworkServerInfo(new[] { "", "   ", null! }, new[] { " " }, null, new[] { new ServerInfoNews { Date = "28 Sep" } }),
        new NetworkServerInfo(null, null, new[] { new ServerInfoLink { Label = "Script", Url = "javascript:void(0)" } }, null),
    };

    // A server that has nothing to show for this join replaces the earlier info with nothing.
    [Fact]
    public void EmptyInfoAfterAnOpenPanel_ClosesIt()
    {
        var popup = new FakePopup();
        using var service = popup.CreateService();
        service.Initialize();
        service.Show(Welcome);
        service.Update();

        service.Show(new NetworkServerInfo(null, null, null, null));
        service.Update();

        Assert.False(popup.ViewModel!.IsOpen);
        Assert.Equal(1, popup.Opened);
        Assert.Contains("Tabs: none", service.Describe());
    }

    [Fact]
    public void DescribeReportsTabsCountsTheDialogAndEachLink()
    {
        var popup = new FakePopup();
        using var service = popup.CreateService();
        service.Initialize();
        service.Show(new NetworkServerInfo(new[] { "Welcome" }, new[] { "No griefing", "Be kind" }, new[]
        {
            new ServerInfoLink { Label = "Discord", Url = "https://discord.gg/example" },
            new ServerInfoLink { Label = "", Url = "https://example.com/" },
            new ServerInfoLink { Label = "Script", Url = "javascript:void(0)" },
        }, null));
        service.Update();
        popup.ViewModel!.ExecuteSelectTab((int)ServerInfoTab.Links);
        popup.ViewModel.Links[0].ExecuteOpen();

        Assert.Equal(
            "Open: True\nPending: False\nTab: Links\nTabs: Motd, Rules, Links\nCounts: motd 1, rules 2, links 2, news 0\n" +
            "Link dialog: open https://discord.gg/example\n" +
            "Link 1: Discord | https://discord.gg/example\nLink 2: https://example.com/ | https://example.com/",
            service.Describe());
    }

    // The overlay is created once and removed with the session.
    [Fact]
    public void InitializeCreatesOneOverlayAndDisposeRemovesIt()
    {
        var popup = new FakePopup();
        var service = popup.CreateService();

        service.Initialize();
        service.Initialize();
        service.Dispose();

        Assert.Equal(1, popup.Created);
        Assert.True(popup.Disposed);
    }

    internal sealed class FakePopup : IServerInfoPopup
    {
        public ServerInfoVM? ViewModel { get; private set; }
        public FakeOpener Opener { get; } = new();
        public bool CanOpenResult { get; set; } = true;
        public int Created { get; private set; }
        public int Opened { get; private set; }
        public int Closed { get; private set; }
        public bool Disposed { get; private set; }

        public ServerInfoService CreateService() => new((viewModel, _) =>
        {
            ViewModel = viewModel;
            Created++;
            return this;
        }, new ServerInfoLinkRules(), Opener);

        public bool CanOpen() => CanOpenResult;

        public void Open()
        {
            Opened++;
            ViewModel!.IsOpen = true;
        }

        public void Close()
        {
            if (!ViewModel!.IsOpen) return;
            Closed++;
            ViewModel.IsOpen = false;
        }

        public void Dispose() => Disposed = true;
    }
}
