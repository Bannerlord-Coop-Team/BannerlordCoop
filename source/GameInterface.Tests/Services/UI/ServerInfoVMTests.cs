using GameInterface.Services.UI.ServerInfo;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GameInterface.Tests.Services.UI;

/// <summary>Protects the panel's tabs, their content and the confirmation before a link opens.</summary>
public class ServerInfoVMTests
{
    private readonly FakeOpener opener = new();
    private int closed;

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

    [Fact]
    public void LinkWithoutALabel_ShowsItsAddress()
    {
        var vm = Create();

        vm.SetContent(new NetworkServerInfo(null, null, new[]
        {
            new ServerInfoLink { Label = "", Url = "https://example.com/rules" },
            new ServerInfoLink { Label = null, Url = "https://discord.gg/example" },
            new ServerInfoLink { Label = "Website", Url = "https://example.com/" },
        }, null));

        Assert.Equal(new[] { ("https://example.com/rules", "https://example.com/rules"), ("https://discord.gg/example", "https://discord.gg/example"), ("Website", "https://example.com/") },
            vm.Links.Select(link => (link.Label, link.Address)));
    }

    // A client must not trust a server: anything the link rules refuse is never shown, and what is shown is normalized.
    [Fact]
    public void BadLinksThatSlippedPastTheServer_AreNotShown()
    {
        var vm = Create();

        vm.SetContent(new NetworkServerInfo(null, null, new[]
        {
            new ServerInfoLink { Label = "Script", Url = "javascript:void(0)" },
            new ServerInfoLink { Label = "File", Url = "file:///C:/Windows/win.ini" },
            new ServerInfoLink { Label = "Login", Url = "https://user@example.com/" },
            new ServerInfoLink { Label = "Relative", Url = "/relative/path" },
            new ServerInfoLink { Label = "Spaces", Url = "https://example.com/ \"--flag" },
            new ServerInfoLink { Label = "Empty", Url = null },
            new ServerInfoLink { Label = "Shop", Url = "https://bücher.example/" },
            null,
        }, null));

        var link = Assert.Single(vm.Links);
        Assert.Equal(("Shop", "https://xn--bcher-kva.example/"), (link.Label, link.Address));
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
        Assert.Equal("https://example.com/", vm.LinkDialogAddress);
        Assert.Contains(nameof(ServerInfoVM.IsLinkDialogOpen), changed);
        Assert.Contains(nameof(ServerInfoVM.LinkDialogAddress), changed);
        Assert.Empty(opener.Opened);
    }

    [Fact]
    public void Open_OpensTheCheckedAddressOnceAndClosesTheDialog()
    {
        var vm = CreateWithLinks();
        vm.Links[0].ExecuteOpen();

        vm.ExecuteOpenLink();
        vm.ExecuteOpenLink();

        Assert.Equal(new[] { "https://xn--bcher-kva.example/pfad" }, opener.Opened);
        Assert.False(vm.IsLinkDialogOpen);
        Assert.Equal(string.Empty, vm.LinkDialogAddress);
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

    private ServerInfoVM Create() => new(() => closed++, new ServerInfoLinkRules(), opener);

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
