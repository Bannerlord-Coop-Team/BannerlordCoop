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
        Assert.Equal("28", container.Attribute("MarginRight")?.Value);
        Assert.Null(container.Attribute("DataSource"));

        var label = Assert.Single(hint.Descendants("TextWidget"));
        Assert.Equal("Incompatible (i)", label.Attribute("Text")?.Value);
        Assert.Equal("#FF8080FF", label.Attribute("Brush.FontColor")?.Value);
    }

    private static string FindMoviePath([CallerFilePath] string sourceFile = "")
    {
        var sourceDirectory = Path.GetDirectoryName(sourceFile);
        return Path.GetFullPath(Path.Combine(sourceDirectory!,
            "..", "..", "..", "..", "UIMovies", "CoopConnectionUIMovie.xml"));
    }
}
