using System.IO;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Xunit;

namespace GameInterface.Tests.Services.UI;

public class CoopConnectionUIMovieTests
{
    [Fact]
    public void IncompatibleStatusHint_KeepsRowBindingsOutsideHintDataSource()
    {
        var document = XDocument.Load(FindMoviePath());
        var hint = Assert.Single(document.Descendants("HintWidget"),
            element => element.Attribute("DataSource")?.Value == "{StatusHint}");

        Assert.Null(hint.Attribute("IsVisible"));

        var container = Assert.IsType<XElement>(hint.Parent?.Parent);
        Assert.Equal("Widget", container.Name.LocalName);
        Assert.Equal("@IsStatusHintVisible", container.Attribute("IsVisible")?.Value);
        Assert.Equal("52", container.Attribute("MarginRight")?.Value);
        Assert.Null(container.Attribute("DataSource"));

        var label = Assert.Single(hint.Descendants("TextWidget"));
        Assert.Equal("Incompatible (i)", label.Attribute("Text")?.Value);
        Assert.Equal("#FF8080FF", label.Attribute("Brush.FontColor")?.Value);

        foreach (var id in new[] { "LobbyColumnHeaders", "LobbyListContainer" })
        {
            var tableSection = Assert.Single(document.Descendants(),
                element => element.Attribute("Id")?.Value == id);
            Assert.Equal("Fixed", tableSection.Attribute("WidthSizePolicy")?.Value);
            Assert.Equal("900", tableSection.Attribute("SuggestedWidth")?.Value);
        }
    }

    [Fact]
    public void SteamLobbyPagination_BindsVisibilityNavigationAndPageText()
    {
        var document = XDocument.Load(FindMoviePath());
        var controls = FindById(document, "LobbyPaginationControls");

        Assert.Equal("@IsSteamLobbyPaginationVisible", controls.Attribute("IsVisible")?.Value);

        var previous = FindById(document, "PreviousLobbyPageButton");
        Assert.Equal("@IsPreviousSteamLobbyPageDisabled", previous.Attribute("IsDisabled")?.Value);
        Assert.Equal("ActionPreviousSteamLobbyPage", previous.Attribute("Command.Click")?.Value);
        Assert.Equal("@PreviousPageButtonText", previous.Attribute("Parameter.Text")?.Value);

        var indicator = FindById(document, "LobbyPageIndicator");
        Assert.Equal("@SteamLobbyPageText", indicator.Attribute("Text")?.Value);

        var next = FindById(document, "NextLobbyPageButton");
        Assert.Equal("@IsNextSteamLobbyPageDisabled", next.Attribute("IsDisabled")?.Value);
        Assert.Equal("ActionNextSteamLobbyPage", next.Attribute("Command.Click")?.Value);
        Assert.Equal("@NextPageButtonText", next.Attribute("Parameter.Text")?.Value);

        Assert.Equal("334", FindById(document, "LobbyListContainer")
            .Attribute("SuggestedHeight")?.Value);
    }

    private static XElement FindById(XDocument document, string id)
    {
        return Assert.Single(document.Descendants(),
            element => element.Attribute("Id")?.Value == id);
    }

    private static string FindMoviePath([CallerFilePath] string sourceFile = "")
    {
        var sourceDirectory = Path.GetDirectoryName(sourceFile);
        return Path.GetFullPath(Path.Combine(sourceDirectory!,
            "..", "..", "..", "..", "UIMovies", "CoopConnectionUIMovie.xml"));
    }
}
