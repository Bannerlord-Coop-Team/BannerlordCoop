using Common.Logging;
using GameInterface.Services.UI.ServerInfo;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GameInterface.Tests.Services.UI;

/// <summary>Protects the panel's tabs, their content and the confirmation before a link opens.</summary>
[Collection(ViewModelCollection.Name)]
public sealed class ServerInfoVMTests : IDisposable
{
    private readonly FakeOpener opener = new();
    private readonly ConcurrentQueue<string> logs = new();
    private readonly Action<string> capture;
    private int closed;

    public ServerInfoVMTests()
    {
        capture = logs.Enqueue;
        OutputSinkManager.AddLogCallback(capture);
    }

    [Fact]
    public void TextsAreTheEnglishDefaults()
    {
        var vm = Create();

        Assert.Equal("Server Info", vm.Title);
        Assert.Equal(new[] { "Message of the Day", "Rules", "Links", "News" }, new[] { vm.MotdTabText, vm.RulesTabText, vm.LinksTabText, vm.NewsTabText });
        Assert.Equal("Type !motd in chat to open this again", vm.HintText);
        Assert.Equal("Close", vm.CloseText);
        Assert.Equal(("Open link", "Open this link in your browser?", "Open", "Cancel"), (vm.LinkDialogTitle, vm.LinkDialogText, vm.OpenLinkText, vm.CancelLinkText));
    }

    // Only tabs with content are shown, always in the same order, and the panel starts on the first one.
    [Theory]
    [InlineData(true, true, true, true, "Motd,Rules,Links,News")]
    [InlineData(false, true, true, true, "Rules,Links,News")]
    [InlineData(false, false, true, false, "Links")]
    [InlineData(true, false, false, true, "Motd,News")]
    [InlineData(false, false, false, true, "News")]
    public void VisibleTabsFollowTheContent(bool motd, bool rules, bool links, bool news, string expected)
    {
        var vm = Create();

        Assert.True(vm.SetContent(Info(motd, rules, links, news)));
        vm.SelectFirstTab();

        Assert.Equal(expected, string.Join(",", vm.VisibleTabs));
        Assert.Equal((motd, rules, links, news), (vm.HasMotd, vm.HasRules, vm.HasLinks, vm.HasNews));
        Assert.Equal(expected.Split(',')[0], vm.SelectedTab.ToString());
        Assert.Equal(1, new[] { vm.IsMotdSelected, vm.IsRulesSelected, vm.IsLinksSelected, vm.IsNewsSelected }.Count(selected => selected));
    }

    [Fact]
    public void NothingToShow_HasNoTabs()
    {
        var vm = Create();

        Assert.False(vm.SetContent(null));
        Assert.False(vm.SetContent(new NetworkServerInfo(null, null, null, null)));
        Assert.False(vm.SetContent(new NetworkServerInfo(new[] { " ", null }, new[] { "" }, new ServerInfoLink[] { null }, new ServerInfoNews[] { null, new ServerInfoNews { Date = "28 Sep" } })));

        Assert.Empty(vm.VisibleTabs);
        Assert.False(vm.IsMotdSelected || vm.IsRulesSelected || vm.IsLinksSelected || vm.IsNewsSelected);
    }

    [Fact]
    public void SelectingATab_ShowsOnlyThatTab()
    {
        var vm = Create();
        vm.SetContent(Info(true, true, true, true));
        var changed = new List<string>();
        vm.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        vm.ExecuteSelectTab((int)ServerInfoTab.News);

        Assert.Equal(ServerInfoTab.News, vm.SelectedTab);
        Assert.Equal((false, false, false, true), (vm.IsMotdSelected, vm.IsRulesSelected, vm.IsLinksSelected, vm.IsNewsSelected));
        Assert.Contains(nameof(ServerInfoVM.IsMotdSelected), changed);
        Assert.Contains(nameof(ServerInfoVM.IsNewsSelected), changed);
    }

    // Tabs switch in any order and as often as the player clicks. After every click exactly that tab is selected,
    // a second click keeps it, and the buttons are told so they follow.
    [Theory]
    [InlineData("Rules,Links,News,Links,Rules,Motd")]
    [InlineData("News,Motd,News,Motd,News")]
    [InlineData("Rules,Rules,Rules,Motd,Motd")]
    [InlineData("Links,Motd,Links,Links,News,Rules,Motd,News,News,Links")]
    public void TabsSwitchBackAndForthInAnyOrder(string clicks)
    {
        var vm = Create();
        vm.SetContent(Info(true, true, true, true));
        vm.SelectFirstTab();
        var changed = new List<string>();
        vm.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        foreach (var tab in clicks.Split(',').Select(name => Enum.Parse<ServerInfoTab>(name)))
        {
            changed.Clear();

            vm.ExecuteSelectTab((int)tab);

            Assert.Equal(tab, vm.SelectedTab);
            Assert.Equal(new[] { tab }, SelectedTabs(vm));
            Assert.Equal(new[] { "IsMotdSelected", "IsRulesSelected", "IsLinksSelected", "IsNewsSelected" }, changed.Where(name => name.EndsWith("Selected", StringComparison.Ordinal)));
        }
    }

    // A tab without content has no button, so clicks between the shown tabs keep working around it.
    [Fact]
    public void SwitchingSkipsTabsWithoutContent()
    {
        var vm = Create();
        vm.SetContent(Info(motd: true, rules: false, links: false, news: true));
        vm.SelectFirstTab();

        var seen = new List<ServerInfoTab>();
        foreach (var tab in new[] { ServerInfoTab.News, ServerInfoTab.Rules, ServerInfoTab.Motd, ServerInfoTab.Links, ServerInfoTab.News, ServerInfoTab.Motd })
        {
            vm.ExecuteSelectTab((int)tab);
            seen.Add(Assert.Single(SelectedTabs(vm)));
        }

        Assert.Equal(new[] { ServerInfoTab.News, ServerInfoTab.News, ServerInfoTab.Motd, ServerInfoTab.Motd, ServerInfoTab.News, ServerInfoTab.Motd }, seen);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(99)]
    [InlineData((int)ServerInfoTab.Rules)]
    public void SelectingAnUnknownOrEmptyTab_IsIgnored(int tab)
    {
        var vm = Create();
        vm.SetContent(Info(motd: true, rules: false, links: true, news: false));
        vm.ExecuteSelectTab((int)ServerInfoTab.Links);

        vm.ExecuteSelectTab(tab);

        Assert.Equal(ServerInfoTab.Links, vm.SelectedTab);
        Assert.True(vm.IsLinksSelected);
    }

    // Blank rules are dropped before numbering, so the numbers never skip.
    [Fact]
    public void RulesAreNumberedFromOne()
    {
        var vm = Create();

        vm.SetContent(new NetworkServerInfo(null, new[] { "Be kind in chat", " ", "No griefing", null, "Ask before joining an army" }, null, null));

        Assert.Equal(new[] { ("1.", "Be kind in chat"), ("2.", "No griefing"), ("3.", "Ask before joining an army") },
            vm.Rules.Select(rule => (rule.Number, rule.Text)));
    }

    [Fact]
    public void NewsShowsItsPartsWithDividersBetweenEntries()
    {
        var vm = Create();

        vm.SetContent(new NetworkServerInfo(null, null, null, new[]
        {
            new ServerInfoNews { Date = "28 Sep 2026", Title = "Siege weekend", Text = "Double renown." },
            new ServerInfoNews { Date = null, Title = "", Text = "Only text" },
            new ServerInfoNews { Date = "  ", Title = null, Text = " " },
            null,
            new ServerInfoNews { Date = "", Title = "Only a title", Text = null },
        }));

        Assert.Equal(new[]
        {
            ("28 Sep 2026", "Siege weekend", "Double renown.", true, true, true, false),
            ("", "", "Only text", false, false, true, true),
            ("", "Only a title", "", false, true, false, true),
        }, vm.News.Select(item => (item.Date, item.Title, item.Text, item.HasDate, item.HasTitle, item.HasText, item.HasDivider)));
    }

    // Operator text is bound as plain strings, so braces and tags reach the screen as written.
    [Fact]
    public void TextIsKeptLiteral()
    {
        const string literal = "{PLAYER} {=coop_server_info_title}x <b>bold</b> <a href=\"event:1\">link</a>";
        var vm = Create();

        vm.SetContent(new NetworkServerInfo(new[] { literal }, new[] { literal },
            new[] { new ServerInfoLink { Label = literal, Url = "https://example.com/" } },
            new[] { new ServerInfoNews { Date = literal, Title = literal, Text = literal } }));

        Assert.Equal(literal, Assert.Single(vm.Paragraphs).Text);
        Assert.Equal(literal, Assert.Single(vm.Rules).Text);
        Assert.Equal(literal, Assert.Single(vm.Links).Label);
        var news = Assert.Single(vm.News);
        Assert.Equal((literal, literal, literal), (news.Date, news.Title, news.Text));
    }

    // The row shows the full address under its label, so a link without a label shows its host rather than the
    // address twice. The host is the punycode one from the checked address, so a look-alike name shows as it is.
    [Fact]
    public void LinkWithoutALabel_ShowsItsHost()
    {
        var vm = Create();

        vm.SetContent(new NetworkServerInfo(null, null, new[]
        {
            new ServerInfoLink { Label = "", Url = "https://example.com/rules" },
            new ServerInfoLink { Label = null, Url = "https://discord.gg/example" },
            new ServerInfoLink { Label = "  ", Url = "https://bücher.example/" },
            new ServerInfoLink { Label = "", Url = "https://status.example.com:8080/status?page=1#top" },
            new ServerInfoLink { Label = "Website", Url = "https://example.com/" },
        }, null));

        Assert.Equal(new[]
            {
                ("example.com", "https://example.com/rules"),
                ("discord.gg", "https://discord.gg/example"),
                ("xn--bcher-kva.example", "https://xn--bcher-kva.example/"),
                ("status.example.com", "https://status.example.com:8080/status?page=1#top"),
                ("Website", "https://example.com/"),
            },
            vm.Links.Select(link => (link.Label, link.Address)));
    }

    // A client must not trust a server: anything the link rules refuse is never shown, and what is shown is normalized.
    // Each refused link is logged by its index, never by its address.
    [Fact]
    public void BadLinksThatSlippedPastTheServer_AreNotShown()
    {
        string marker = Guid.NewGuid().ToString("N");
        var vm = Create();

        vm.SetContent(new NetworkServerInfo(null, null, new[]
        {
            new ServerInfoLink { Label = "Script", Url = "javascript:void('" + marker + "')" },
            new ServerInfoLink { Label = "File", Url = "file:///C:/" + marker + "/win.ini" },
            new ServerInfoLink { Label = "Login", Url = "https://user@example.com/" + marker },
            new ServerInfoLink { Label = "Relative", Url = "/relative/" + marker },
            new ServerInfoLink { Label = "Spaces", Url = "https://example.com/ \"--flag " + marker },
            new ServerInfoLink { Label = "Empty", Url = null },
            new ServerInfoLink { Label = "Shop", Url = "https://bücher.example/" + marker },
            null,
        }, null));

        var link = Assert.Single(vm.Links);
        Assert.Equal(("Shop", "https://xn--bcher-kva.example/" + marker), (link.Label, link.Address));
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 7 }, LoggedLinkIndexes());
        Assert.DoesNotContain(logs, log => log.Contains(marker));
    }

    // Only the first MaxLinks entries are checked, so a server sending many bad links adds at most MaxLinks log lines.
    [Fact]
    public void ManyBadLinks_AreCheckedOnlyUpToTheLinkCap()
    {
        var vm = Create();
        var links = Enumerable.Range(0, 30).Select(index => new ServerInfoLink { Label = "Script", Url = "javascript:void(" + index + ")" }).ToList();
        links.Add(new ServerInfoLink { Label = "Website", Url = "https://example.com/" });

        Assert.True(vm.SetContent(new NetworkServerInfo(new[] { "Welcome" }, null, links.ToArray(), null)));

        Assert.Empty(vm.Links);
        Assert.Equal(Enumerable.Range(0, ServerInfoLimits.MaxLinks), LoggedLinkIndexes());
    }

    [Fact]
    public void OnlyBadLinks_LeaveNoLinksTab()
    {
        var vm = Create();

        Assert.True(vm.SetContent(new NetworkServerInfo(new[] { "Welcome" }, null, new[] { new ServerInfoLink { Label = "Script", Url = "javascript:void(0)" } }, null)));

        Assert.Equal(new[] { ServerInfoTab.Motd }, vm.VisibleTabs);
    }

    // The server caps are applied again, so a server sending more still builds a bounded panel.
    [Fact]
    public void ClientKeepsAtMostTheServerCaps()
    {
        var vm = Create();

        vm.SetContent(new NetworkServerInfo(
            Enumerable.Range(0, 30).Select(index => "Paragraph " + index).ToArray(),
            Enumerable.Range(0, 30).Select(index => "Rule " + index).ToArray(),
            Enumerable.Range(0, 30).Select(index => new ServerInfoLink { Label = "Link", Url = "https://example.com/" + index }).ToArray(),
            Enumerable.Range(0, 30).Select(index => new ServerInfoNews { Title = "News " + index }).ToArray()));

        Assert.Equal(
            (ServerInfoLimits.MaxMotdParagraphs, ServerInfoLimits.MaxRules, ServerInfoLimits.MaxLinks, ServerInfoLimits.MaxNews),
            (vm.Paragraphs.Count, vm.Rules.Count, vm.Links.Count, vm.News.Count));
    }

    [Fact]
    public void ClickingALink_AsksFirstAndOpensNothing()
    {
        var vm = CreateWithLinks();
        var changed = new List<string>();
        vm.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        vm.Links[1].ExecuteOpen();

        Assert.True(vm.IsLinkDialogOpen);
        Assert.False(vm.IsOpenLinkEnabled);
        Assert.Equal("https://example.com/", vm.LinkDialogAddress);
        Assert.Contains(nameof(ServerInfoVM.IsLinkDialogOpen), changed);
        Assert.Contains(nameof(ServerInfoVM.LinkDialogAddress), changed);
        Assert.Contains(nameof(ServerInfoVM.IsOpenLinkEnabled), changed);
        Assert.Empty(opener.Opened);
    }

    [Fact]
    public void Open_OpensTheCheckedAddressOnceAndClosesTheDialog()
    {
        var vm = CreateWithLinks();
        vm.Links[0].ExecuteOpen();
        WaitOutTheOpenDelay(vm);

        vm.ExecuteOpenLink();
        vm.ExecuteOpenLink();

        Assert.Equal(new[] { "https://xn--bcher-kva.example/pfad" }, opener.Opened);
        Assert.False(vm.IsLinkDialogOpen);
        Assert.False(vm.IsOpenLinkEnabled);
        Assert.Equal(string.Empty, vm.LinkDialogAddress);
        Assert.Equal(0, closed);
    }

    // The dialog opens under the pointer, so the second click of a double-click on a row, or a quick run of clicks,
    // reaches Open before the player has read the address. Open takes no click until the wait is over.
    [Fact]
    public void OpenRightAfterTheDialogAppears_OpensNothing()
    {
        var vm = CreateWithLinks();
        vm.Links[0].ExecuteOpen();

        vm.ExecuteOpenLink();
        vm.Tick(ServerInfoVM.MaxOpenLinkTickSeconds);
        vm.ExecuteOpenLink();

        Assert.Empty(opener.Opened);
        Assert.True(vm.IsLinkDialogOpen);
        Assert.False(vm.IsOpenLinkEnabled);
    }

    // Four frames of an eighth of a second make the half second, and the button is told once when it turns on.
    [Fact]
    public void OpenTurnsOnAfterTheDelayAndOpensOnce()
    {
        var vm = CreateWithLinks();
        vm.Links[0].ExecuteOpen();
        var changed = new List<string>();
        vm.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        int frames = (int)(ServerInfoVM.OpenLinkDelaySeconds / ServerInfoVM.MaxOpenLinkTickSeconds);

        for (int frame = 1; frame < frames; frame++) vm.Tick(ServerInfoVM.MaxOpenLinkTickSeconds);
        Assert.False(vm.IsOpenLinkEnabled);
        Assert.Empty(changed);

        vm.Tick(ServerInfoVM.MaxOpenLinkTickSeconds);
        vm.Tick(ServerInfoVM.MaxOpenLinkTickSeconds);

        Assert.True(vm.IsOpenLinkEnabled);
        Assert.Equal(new[] { nameof(ServerInfoVM.IsOpenLinkEnabled) }, changed);
        vm.ExecuteOpenLink();
        vm.ExecuteOpenLink();
        Assert.Equal(new[] { "https://xn--bcher-kva.example/pfad" }, opener.Opened);
    }

    // A hitch while the player double-clicks must not end the wait in one frame, and a frame of zero, below zero or
    // NaN does not count at all.
    [Fact]
    public void LongOrBrokenFrames_DoNotEndTheWaitEarly()
    {
        var vm = CreateWithLinks();
        vm.Links[0].ExecuteOpen();

        vm.Tick(10f);
        vm.Tick(0f);
        vm.Tick(-1f);
        vm.Tick(float.NaN);
        vm.ExecuteOpenLink();

        Assert.False(vm.IsOpenLinkEnabled);
        Assert.Empty(opener.Opened);

        vm.Tick(float.PositiveInfinity);
        vm.Tick(ServerInfoVM.MaxOpenLinkTickSeconds);
        vm.Tick(ServerInfoVM.MaxOpenLinkTickSeconds);

        Assert.True(vm.IsOpenLinkEnabled);
    }

    // Every time the dialog appears, for the same link or another, Open waits again.
    [Fact]
    public void ReopeningTheDialog_WaitsAgain()
    {
        var vm = CreateWithLinks();
        vm.Links[0].ExecuteOpen();
        WaitOutTheOpenDelay(vm);
        vm.ExecuteCancelLink();

        vm.Links[0].ExecuteOpen();
        vm.ExecuteOpenLink();
        Assert.False(vm.IsOpenLinkEnabled);

        WaitOutTheOpenDelay(vm);
        vm.HandleEscape();
        vm.Links[1].ExecuteOpen();
        vm.ExecuteOpenLink();
        Assert.False(vm.IsOpenLinkEnabled);
        Assert.Empty(opener.Opened);

        WaitOutTheOpenDelay(vm);
        vm.ExecuteOpenLink();
        Assert.Equal(new[] { "https://example.com/" }, opener.Opened);
    }

    // Cancel and Escape never wait.
    [Fact]
    public void CancelAndEscapeWorkDuringTheWait()
    {
        var vm = CreateWithLinks();

        vm.Links[0].ExecuteOpen();
        vm.ExecuteCancelLink();
        Assert.False(vm.IsLinkDialogOpen);

        vm.Links[0].ExecuteOpen();
        vm.HandleEscape();
        Assert.False(vm.IsLinkDialogOpen);
        Assert.Equal(0, closed);
    }

    [Fact]
    public void Cancel_ClosesOnlyTheDialog()
    {
        var vm = CreateWithLinks();
        vm.Links[0].ExecuteOpen();

        vm.ExecuteCancelLink();

        Assert.False(vm.IsLinkDialogOpen);
        Assert.Empty(opener.Opened);
        Assert.Equal(0, closed);
    }

    [Fact]
    public void Escape_ClosesTheDialogFirstAndThenThePanel()
    {
        var vm = CreateWithLinks();
        vm.Links[0].ExecuteOpen();

        vm.HandleEscape();

        Assert.False(vm.IsLinkDialogOpen);
        Assert.Equal(0, closed);

        vm.HandleEscape();

        Assert.Equal(1, closed);
        Assert.Empty(opener.Opened);
    }

    // Anything that takes the dialog away without the Open click leaves the browser alone.
    [Fact]
    public void DialogClosedAnyOtherWay_OpensNothing()
    {
        var vm = CreateWithLinks();

        vm.Links[0].ExecuteOpen();
        vm.ExecuteSelectTab((int)ServerInfoTab.Motd);
        Assert.False(vm.IsLinkDialogOpen);

        vm.ExecuteSelectTab((int)ServerInfoTab.Links);
        vm.Links[0].ExecuteOpen();
        vm.IsOpen = false;
        Assert.False(vm.IsLinkDialogOpen);

        vm.Links[0].ExecuteOpen();
        vm.SetContent(Info(true, false, true, false));
        Assert.False(vm.IsLinkDialogOpen);

        vm.Links[0].ExecuteOpen();
        vm.SelectFirstTab();
        Assert.False(vm.IsLinkDialogOpen);

        vm.ExecuteOpenLink();
        Assert.Empty(opener.Opened);
    }

    [Fact]
    public void NewContent_KeepsTheSelectedTabWhenItStillHasContent()
    {
        var vm = Create();
        vm.SetContent(Info(true, true, true, true));
        vm.ExecuteSelectTab((int)ServerInfoTab.Rules);

        vm.SetContent(Info(true, true, false, false));
        Assert.Equal(ServerInfoTab.Rules, vm.SelectedTab);

        vm.SetContent(Info(false, false, true, true));
        Assert.Equal(ServerInfoTab.Links, vm.SelectedTab);
    }

    [Fact]
    public void CloseCallsTheOverlay()
    {
        var vm = Create();

        vm.ExecuteClose();

        Assert.Equal(1, closed);
    }

    public void Dispose()
    {
        OutputSinkManager.RemoveLogCallback(capture);
    }

    private ServerInfoVM Create() => new(() => closed++, new ServerInfoLinkRules(), opener);

    // The overlay's frames while the player reads the dialog.
    internal static void WaitOutTheOpenDelay(ServerInfoVM vm)
    {
        for (float waited = 0f; waited < ServerInfoVM.OpenLinkDelaySeconds; waited += ServerInfoVM.MaxOpenLinkTickSeconds)
            vm.Tick(ServerInfoVM.MaxOpenLinkTickSeconds);
    }

    // The indexes this test's view model logged as refused, in order.
    private IEnumerable<int> LoggedLinkIndexes()
    {
        const string prefix = "Server info link at index ";
        const string suffix = " from the server has no usable http or https url; skipping it";
        return logs.Where(log => log.StartsWith(prefix, StringComparison.Ordinal) && log.EndsWith(suffix, StringComparison.Ordinal))
            .Select(log => int.Parse(log.Substring(prefix.Length, log.Length - prefix.Length - suffix.Length)))
            .ToArray();
    }

    private ServerInfoVM CreateWithLinks()
    {
        var vm = Create();
        vm.SetContent(new NetworkServerInfo(new[] { "Welcome" }, null, new[]
        {
            new ServerInfoLink { Label = "Shop", Url = "https://bücher.example/pfad" },
            new ServerInfoLink { Label = "Website", Url = "https://example.com/" },
        }, null));
        vm.ExecuteSelectTab((int)ServerInfoTab.Links);
        vm.IsOpen = true;
        return vm;
    }

    // The tabs whose selected flag the buttons read, in the order they are shown.
    private static IEnumerable<ServerInfoTab> SelectedTabs(ServerInfoVM vm) =>
        new[] { (ServerInfoTab.Motd, vm.IsMotdSelected), (ServerInfoTab.Rules, vm.IsRulesSelected), (ServerInfoTab.Links, vm.IsLinksSelected), (ServerInfoTab.News, vm.IsNewsSelected) }
            .Where(entry => entry.Item2).Select(entry => entry.Item1).ToArray();

    internal static NetworkServerInfo Info(bool motd, bool rules, bool links, bool news) => new(
        motd ? new[] { "Welcome to EU-1" } : null,
        rules ? new[] { "No griefing" } : null,
        links ? new[] { new ServerInfoLink { Label = "Discord", Url = "https://discord.gg/example" } } : null,
        news ? new[] { new ServerInfoNews { Date = "28 Sep 2026", Title = "Siege weekend", Text = "Double renown." } } : null);

    internal sealed class FakeOpener : IBrowserLinkOpener
    {
        public List<string> Opened { get; } = new();

        public void Open(string address) => Opened.Add(address);
    }
}
