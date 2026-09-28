using GameInterface.Services.UI.ServerInfo;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace GameInterface.Tests.Services.UI;

/// <summary>Protects the panel's movie: its bindings, commands, plain text widgets and brushes.</summary>
public class ServerInfoUIMovieTests
{
    private static readonly Dictionary<string, Type> ItemTypes = new()
    {
        ["{Paragraphs}"] = typeof(ServerInfoParagraphVM),
        ["{Rules}"] = typeof(ServerInfoRuleVM),
        ["{Links}"] = typeof(ServerInfoLinkVM),
        ["{News}"] = typeof(ServerInfoNewsVM),
    };

    private readonly XDocument document = XDocument.Load(FindRepositoryFile("UIMovies", "CoopServerInfoUIMovie.xml"));

    // Every binding names a member of the view model it binds to, so nothing shows blank in game.
    [Fact]
    public void BindingsMatchTheViewModels()
    {
        Assert.Equal(ItemTypes.Keys.OrderBy(key => key), document.Descendants().Select(element => element.Attribute("DataSource")?.Value)
            .Where(source => source != null).OrderBy(source => source));

        foreach (var (element, property) in Bindings())
        {
            var list = element.Ancestors("ItemTemplate").FirstOrDefault()?.Parent;
            var type = list == null ? typeof(ServerInfoVM) : ItemTypes[list.Attribute("DataSource")!.Value];
            Assert.True(type.GetProperty(property) != null, $"{type.Name}.{property}");
        }

        foreach (var element in document.Descendants().Where(element => element.Attribute("Command.Click") != null))
        {
            var list = element.Ancestors("ItemTemplate").FirstOrDefault()?.Parent;
            var type = list == null ? typeof(ServerInfoVM) : ItemTypes[list.Attribute("DataSource")!.Value];
            string command = element.Attribute("Command.Click")!.Value;
            Assert.True(type.GetMethods().Count(method => method.Name == command) == 1, $"{type.Name}.{command}");
        }
    }

    // Operator text goes to plain text widgets; a rich text widget would read tags and links in it.
    [Fact]
    public void OperatorTextUsesPlainTextWidgets()
    {
        Assert.Empty(document.Descendants().Where(element => element.Name.LocalName.Contains("RichText")));
        var textWidgets = document.Descendants().Where(element => element.Attribute("Text") != null).ToArray();

        Assert.NotEmpty(textWidgets);
        Assert.All(textWidgets, element => Assert.Contains(element.Name.LocalName, new[] { "TextWidget", "ScrollingTextWidget" }));
    }

    [Fact]
    public void TabsSelectTheirOwnTabInOrder()
    {
        var tabs = document.Descendants("ButtonWidget").Where(element => element.Attribute("Id")?.Value.StartsWith("ServerInfoTab", StringComparison.Ordinal) == true).ToArray();

        Assert.Equal(new[] { "ServerInfoTabMotd", "ServerInfoTabRules", "ServerInfoTabLinks", "ServerInfoTabNews" }, tabs.Select(tab => tab.Attribute("Id")!.Value));
        for (int i = 0; i < tabs.Length; i++)
        {
            var tab = (ServerInfoTab)i;
            Assert.Equal(nameof(ServerInfoVM.ExecuteSelectTab), tabs[i].Attribute("Command.Click")?.Value);
            Assert.Equal(i.ToString(), tabs[i].Attribute("CommandParameter.Click")?.Value);
            Assert.Equal("@Is" + tab + "Selected", tabs[i].Attribute("IsSelected")?.Value);
            Assert.Equal("@Has" + tab, tabs[i].Attribute("IsVisible")?.Value);
            // The game's tab brush has its own hover, press and selected looks, and the text follows the button's state.
            Assert.Equal("Header.Tab.Center", tabs[i].Attribute("Brush")?.Value);
            Assert.Equal("true", tabs[i].Attribute("UpdateChildrenStates")?.Value);
            // A toggle or radio button would change its own selection; a plain one leaves it to the view model,
            // so a second click keeps the tab selected.
            Assert.Null(tabs[i].Attribute("ButtonType"));
        }
    }

    // The game hands a press to the last drawn widget under the pointer that accepts events, even one with no
    // command (EventManager.CollectEnableWidgetsAt). So whatever the panel draws after the tabs passes presses
    // on or sits below the tab row, and only the open link dialog covers them.
    [Fact]
    public void NothingDrawnAfterTheTabsTakesTheirPresses()
    {
        var tabs = Widget("ServerInfoTabs");
        int tabRowBottom = Number(tabs, "MarginTop") + Number(tabs, "SuggestedHeight");
        var drawnAfter = Widget("CoopServerInfoRoot").Element("Children")!.Elements().SkipWhile(element => element != tabs).Skip(1).ToArray();

        Assert.Contains(Widget("Content"), drawnAfter);
        foreach (var element in drawnAfter)
        {
            string id = element.Attribute("Id")?.Value ?? element.Name.LocalName;
            if (id == "ServerInfoLinkDialog") Assert.Equal("@IsLinkDialogOpen", element.Attribute("IsVisible")?.Value);
            else if (id == "ServerInfoClose") Assert.Equal("Bottom", element.Attribute("VerticalAlignment")?.Value);
            else Assert.True(element.Attribute("DoNotAcceptEvents")?.Value == "true", id + " takes the presses meant for the tabs");
        }

        // Inside those, the tab bodies and the scrollbars start below the tab row.
        Assert.True(Number(Widget("Content").Element("Children")!.Elements().First(), "SuggestedHeight") >= tabRowBottom);
        Assert.All(document.Descendants("ScrollbarWidget"), scrollbar => Assert.True(Number(scrollbar, "MarginTop") >= tabRowBottom));
    }

    // A button's own text or icons would take its presses before it, so every button keeps them.
    [Fact]
    public void EveryButtonTakesItsOwnPresses()
    {
        var buttons = document.Descendants("ButtonWidget").ToArray();

        Assert.Equal(
            new[] { "ServerInfoTabMotd", "ServerInfoTabRules", "ServerInfoTabLinks", "ServerInfoTabNews", "ServerInfoLinkRow", "ServerInfoClose", "ServerInfoLinkOpen", "ServerInfoLinkCancel" },
            buttons.Select(button => button.Attribute("Id")?.Value));
        Assert.All(buttons, button => Assert.Equal("true", button.Attribute("DoNotPassEventsToChildren")?.Value));
    }

    // Each tab body and its scrollbar show only with their tab, so a hidden tab never leaves a scrollbar behind.
    [Theory]
    [InlineData("Motd")]
    [InlineData("Rules")]
    [InlineData("Links")]
    [InlineData("News")]
    public void EachTabHasItsOwnScrollAreaAndScrollbar(string tab)
    {
        var body = Widget("ServerInfo" + tab + "Body");
        var scroll = Widget("ServerInfo" + tab + "Scroll");
        var scrollbarArea = Widget("ServerInfo" + tab + "ScrollbarArea");

        Assert.Equal("@Is" + tab + "Selected", body.Attribute("IsVisible")?.Value);
        Assert.Equal("@Is" + tab + "Selected", scrollbarArea.Attribute("IsVisible")?.Value);
        Assert.Contains(scroll, body.Descendants());
        Assert.Equal(@"..\..\..\ServerInfo" + tab + @"ScrollbarArea\ServerInfo" + tab + "Scrollbar", scroll.Attribute("VerticalScrollbar")?.Value);
        Assert.Contains(Widget("ServerInfo" + tab + "Scrollbar"), scrollbarArea.Elements("Children").Elements());
        Assert.Equal("364", Widget("ServerInfo" + tab + "Clip").Attribute("MaxHeight")?.Value);
        // The area only places the scrollbar, and the game drags the bar when it or its handle is pressed.
        Assert.Equal("true", scrollbarArea.Attribute("DoNotAcceptEvents")?.Value);
        var scrollbar = Widget("ServerInfo" + tab + "Scrollbar");
        var handle = Assert.Single(scrollbar.Element("Children")!.Elements());
        Assert.Equal(scrollbar.Attribute("Handle")?.Value, handle.Attribute("Id")?.Value);
        Assert.Null(scrollbar.Attribute("DoNotAcceptEvents"));
        Assert.Null(handle.Attribute("DoNotAcceptEvents"));
        // The scrollbar draws after the rows and Close after the scrollbar, so none of them overlap.
        Assert.True(Number(scroll, "MarginRight") >= Number(scrollbar, "MarginRight") + Number(scrollbar, "SuggestedWidth"));
        var close = Widget("ServerInfoClose");
        Assert.True(Number(scrollbar, "MarginBottom") >= Number(close, "MarginBottom") + Number(close, "SuggestedHeight"));
    }

    // The dialog sits last so it draws above the rows, and its backdrop takes the clicks meant for them.
    [Fact]
    public void LinkDialogCoversThePanelUntilOpenOrCancel()
    {
        var root = Widget("CoopServerInfoRoot");
        var dialog = Widget("ServerInfoLinkDialog");

        Assert.Same(dialog, root.Element("Children")!.Elements().Last());
        Assert.Equal("@IsLinkDialogOpen", dialog.Attribute("IsVisible")?.Value);
        Assert.Null(Widget("ServerInfoLinkDialogBackdrop").Attribute("DoNotAcceptEvents"));
        // The box draws after the backdrop, so its Open and Cancel buttons get their own presses.
        Assert.Equal(new[] { "ServerInfoLinkDialogBackdrop", "ServerInfoLinkDialogBox" },
            dialog.Element("Children")!.Elements().Select(element => element.Attribute("Id")?.Value));
        Assert.Contains(Widget("ServerInfoLinkOpen"), Widget("ServerInfoLinkDialogBox").Descendants());
        Assert.Contains(Widget("ServerInfoLinkCancel"), Widget("ServerInfoLinkDialogBox").Descendants());
        Assert.Equal(nameof(ServerInfoVM.ExecuteOpenLink), Widget("ServerInfoLinkOpen").Attribute("Command.Click")?.Value);
        Assert.Equal(nameof(ServerInfoVM.ExecuteCancelLink), Widget("ServerInfoLinkCancel").Attribute("Command.Click")?.Value);
        // The game skips a disabled button when it hands out a press, so Open takes none until the view model's
        // wait is over. Cancel never waits.
        Assert.Equal("@" + nameof(ServerInfoVM.IsOpenLinkEnabled), Widget("ServerInfoLinkOpen").Attribute("IsEnabled")?.Value);
        Assert.Null(Widget("ServerInfoLinkCancel").Attribute("IsEnabled"));
        Assert.Equal("@LinkDialogAddress", Widget("ServerInfoLinkDialogAddress").Attribute("Text")?.Value);
        Assert.Equal(nameof(ServerInfoLinkVM.ExecuteOpen), Widget("ServerInfoLinkRow").Attribute("Command.Click")?.Value);
    }

    // Reuses the player list's brushes and the game's own tab and popup ones; no new brushes or sprites.
    [Fact]
    public void MovieUsesOnlyExistingBrushes()
    {
        var coopBrushes = XDocument.Load(FindRepositoryFile("deploy", "GUI", "Brushes", "CoopPlayerList.xml"))
            .Descendants("Brush").Select(brush => brush.Attribute("Name")!.Value).ToHashSet();
        var vanillaBrushes = new HashSet<string>
        {
            "Frame1Brush", "ScoreboardUnitRowBrush", "Header.Tab.Center", "Clan.TabControl.Text",
            "Popup.Done.Button", "Popup.Cancel.Button", "Popup.Button.Text",
        };

        var used = document.Descendants().Select(element => element.Attribute("Brush")?.Value)
            .Where(brush => brush != null).Distinct().ToArray();

        Assert.All(used, brush => Assert.True(coopBrushes.Contains(brush!) || vanillaBrushes.Contains(brush!), brush));
        Assert.Contains("Coop.PlayerList.Title.Background", used);
        Assert.Contains("Header.Tab.Center", used);
    }

    private XElement Widget(string id) => Assert.Single(document.Descendants(), element => element.Attribute("Id")?.Value == id);

    private static int Number(XElement element, string attribute) =>
        int.Parse(element.Attribute(attribute)?.Value ?? throw new InvalidOperationException(attribute + " is missing"), CultureInfo.InvariantCulture);

    private IEnumerable<(XElement Element, string Property)> Bindings() =>
        document.Descendants().SelectMany(element => element.Attributes()
            .Where(attribute => attribute.Value.StartsWith("@", StringComparison.Ordinal))
            .Select(attribute => (element, attribute.Value.Substring(1))));

    private static string FindRepositoryFile(string first, params string[] rest)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Deploy.targets"))) directory = directory.Parent;
        Assert.NotNull(directory);
        return Path.Combine(new[] { directory!.FullName, first }.Concat(rest).ToArray());
    }
}
