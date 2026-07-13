using System.IO;
using System.Linq;
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
        var hint = Assert.Single(document.Descendants("HintWidget")
            .Where(element => (string)element.Attribute("DataSource") == "{StatusHint}"));

        Assert.Null(hint.Attribute("IsVisible"));

        var container = Assert.IsType<XElement>(hint.Parent?.Parent);
        Assert.Equal("Widget", container.Name.LocalName);
        Assert.Equal("@IsStatusHintVisible", (string)container.Attribute("IsVisible"));
        Assert.Null(container.Attribute("DataSource"));

        var label = Assert.Single(hint.Descendants("TextWidget"));
        Assert.Equal("Incompatible (i)", (string)label.Attribute("Text"));
        Assert.Equal("#FF8080FF", (string)label.Attribute("Brush.FontColor"));
    }

    private static string FindMoviePath([CallerFilePath] string sourceFile = "")
    {
        var sourceDirectory = Path.GetDirectoryName(sourceFile);
        return Path.GetFullPath(Path.Combine(sourceDirectory!,
            "..", "..", "..", "..", "UIMovies", "CoopConnectionUIMovie.xml"));
    }
}
