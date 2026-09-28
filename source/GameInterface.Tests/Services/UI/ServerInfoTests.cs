using GameInterface.Services.UI.ServerInfo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Xunit;

namespace GameInterface.Tests.Services.UI;

/// <summary>Protects the message of the day popup: literal text, show-once rules and its wire message.</summary>
public class ServerInfoTests
{
    private static readonly string[] Welcome = { "Welcome to EU-1", "Restart 06:00 UTC" };

    // Operator text is shown as written; braces and tags are not expanded or interpreted.
    [Fact]
    public void ViewModelKeepsParagraphsLiteralAndCloses()
    {
        var closed = 0;
        var vm = new ServerInfoVM(() => closed++);
        var literal = "{PLAYER} {=coop_motd_title}x <b>bold</b> <a href=\"event:1\">link</a>";

        vm.SetParagraphs(new[] { literal, "Second" });

        Assert.Equal("Message of the Day", vm.Title);
        Assert.Equal("Close", vm.CloseText);
        Assert.Equal(new[] { literal, "Second" }, vm.Paragraphs.Select(paragraph => paragraph.Text));
        vm.SetParagraphs(new[] { "Replaced" });
        Assert.Equal("Replaced", Assert.Single(vm.Paragraphs).Text);
        vm.ExecuteClose();
        Assert.Equal(1, closed);
    }

    // A message that arrives before the campaign map or while something else has focus waits for it.
    [Fact]
    public void MessageWaitsUntilTheMapIsAvailable()
    {
        var popup = new FakePopup { CanOpenResult = false };
        using var service = popup.CreateService();

        service.Show(Welcome);
        service.Update();
        Assert.Null(popup.ViewModel);
        Assert.Contains("Pending: 2 paragraph(s)", service.Describe());

        service.Initialize();
        service.Update();
        Assert.False(popup.ViewModel!.IsOpen);
        Assert.Contains("Pending: 2 paragraph(s)", service.Describe());

        popup.CanOpenResult = true;
        service.Update();
        Assert.True(popup.ViewModel.IsOpen);
        Assert.Equal(Welcome, popup.ViewModel.Paragraphs.Select(paragraph => paragraph.Text));
        Assert.Equal("Open: True\nPending: none\nShown: 2 paragraph(s)\n1: Welcome to EU-1\n2: Restart 06:00 UTC", service.Describe());
    }

    // Closing by Escape, the button or leaving the map never reopens the same message.
    [Fact]
    public void ClosedMessageStaysClosed()
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
        Assert.Contains("Pending: none", service.Describe());
    }

    // Each successful join sends the message again, so a rejoin shows it again.
    [Fact]
    public void NewJoinShowsTheMessageAgain()
    {
        var popup = new FakePopup();
        using var service = popup.CreateService();
        service.Initialize();
        service.Show(Welcome);
        service.Update();
        popup.ViewModel!.ExecuteClose();

        service.Show(new[] { "Welcome back" });
        service.Update();

        Assert.True(popup.ViewModel.IsOpen);
        Assert.Equal(2, popup.Opened);
        Assert.Equal("Welcome back", Assert.Single(popup.ViewModel.Paragraphs).Text);
    }

    // Two messages before the map is free show once with the newest text; one while open replaces it in place.
    [Fact]
    public void NewestMessageWinsWithoutASecondPopup()
    {
        var popup = new FakePopup { CanOpenResult = false };
        using var service = popup.CreateService();
        service.Initialize();
        service.Show(new[] { "First" });
        service.Show(new[] { "Second" });
        popup.CanOpenResult = true;
        service.Update();
        service.Show(new[] { "Third" });
        service.Update();

        Assert.Equal(1, popup.Opened);
        Assert.Equal("Third", Assert.Single(popup.ViewModel!.Paragraphs).Text);
        Assert.Contains("Pending: none", service.Describe());
    }

    // No MOTD, an empty one or only blank paragraphs never open an empty popup.
    [Theory]
    [MemberData(nameof(EmptyMessages))]
    public void EmptyMessageShowsNothing(string[] paragraphs)
    {
        var popup = new FakePopup();
        using var service = popup.CreateService();
        service.Initialize();

        service.Show(paragraphs);
        service.Update();

        Assert.False(popup.ViewModel!.IsOpen);
        Assert.Equal(0, popup.Opened);
        Assert.Equal("Open: False\nPending: none\nShown: 0 paragraph(s)", service.Describe());
    }

    public static TheoryData<string[]> EmptyMessages => new()
    {
        null!,
        Array.Empty<string>(),
        new[] { "", "   ", null! },
    };

    // Blank paragraphs are dropped and the rest keep their order.
    [Fact]
    public void BlankParagraphsAreDropped()
    {
        var popup = new FakePopup();
        using var service = popup.CreateService();
        service.Initialize();

        service.Show(new[] { "", "Welcome to EU-1", " ", "Restart 06:00 UTC" });
        service.Update();

        Assert.Equal(Welcome, popup.ViewModel!.Paragraphs.Select(paragraph => paragraph.Text));
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

    // Binds only to members the view models have and shows paragraphs with the non-markup text widget.
    [Fact]
    public void MovieBindsToTheViewModelsAndShowsPlainText()
    {
        var document = XDocument.Load(FindRepositoryFile("UIMovies", "CoopServerInfoUIMovie.xml"));
        var itemTemplate = Assert.Single(document.Descendants("ItemTemplate"));
        var paragraph = Assert.Single(itemTemplate.Elements());

        Assert.Equal("TextWidget", paragraph.Name.LocalName);
        Assert.Equal("@Text", paragraph.Attribute("Text")?.Value);
        Assert.Empty(document.Descendants().Where(element => element.Name.LocalName.Contains("RichText")));
        Assert.Equal("{Paragraphs}", itemTemplate.Parent!.Attribute("DataSource")?.Value);
        Assert.Contains(document.Descendants(), element => element.Attribute("Command.Click")?.Value == nameof(ServerInfoVM.ExecuteClose));
        foreach (var binding in Bindings(document).Where(binding => !binding.Element.Ancestors("ItemTemplate").Any()))
            Assert.NotNull(typeof(ServerInfoVM).GetProperty(binding.Property));
        foreach (var binding in Bindings(document).Where(binding => binding.Element.Ancestors("ItemTemplate").Any()))
            Assert.NotNull(typeof(ServerInfoParagraphVM).GetProperty(binding.Property));
    }

    // Reuses the player list's brushes; the vanilla ones ship with Native, the co-op ones with the mod.
    [Fact]
    public void MovieUsesOnlyExistingBrushes()
    {
        var document = XDocument.Load(FindRepositoryFile("UIMovies", "CoopServerInfoUIMovie.xml"));
        var coopBrushes = XDocument.Load(FindRepositoryFile("deploy", "GUI", "Brushes", "CoopPlayerList.xml"))
            .Descendants("Brush").Select(brush => brush.Attribute("Name")!.Value).ToHashSet();
        var vanillaBrushes = new HashSet<string> { "Frame1Brush", "ScoreboardUnitRowBrush" };

        var used = document.Descendants().Select(element => element.Attribute("Brush")?.Value)
            .Where(brush => brush != null).Distinct().ToArray();

        Assert.NotEmpty(used);
        Assert.All(used, brush => Assert.True(coopBrushes.Contains(brush!) || vanillaBrushes.Contains(brush!), brush));
        Assert.Contains("Coop.PlayerList.Title.Background", used);
    }

#if DEBUG
    // The preview samples stay inside the server caps, and the long one fills the paragraph cap.
    [Fact]
    public void PreviewSamplesFitTheServerCaps()
    {
        Assert.InRange(ServerInfoDebugCommands.SampleParagraphs.Length, 2, ServerInfoLimits.MaxMotdParagraphs);
        Assert.Equal(ServerInfoLimits.MaxMotdParagraphs, ServerInfoDebugCommands.LongSampleParagraphs.Length);
        Assert.InRange(ServerInfoDebugCommands.LongSampleParagraphs.Sum(paragraph => paragraph.Length), 1, ServerInfoLimits.MaxMotdLength);
        var registry = new Common.Commands.CoopCommandRegistry(
            new Common.Commands.ICoopCommand[] { new ServerInfoDebugCommands.ServerInfoPreviewCoopCommand(), new ServerInfoDebugCommands.ServerInfoStateCoopCommand() },
            new Serilog.LoggerConfiguration().CreateLogger());
        Assert.True(registry.Contains("coop.debug.ui.motd_preview"));
        Assert.True(registry.Contains("coop.debug.ui.motd_state"));
    }
#endif

    private static IEnumerable<(XElement Element, string Property)> Bindings(XDocument document) =>
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

    private sealed class FakePopup : IServerInfoPopup
    {
        public ServerInfoVM? ViewModel { get; private set; }
        public bool CanOpenResult { get; set; } = true;
        public int Created { get; private set; }
        public int Opened { get; private set; }
        public int Closed { get; private set; }
        public bool Disposed { get; private set; }

        public ServerInfoService CreateService() => new ServerInfoService((viewModel, _) =>
        {
            ViewModel = viewModel;
            Created++;
            return this;
        });

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
