using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace GameInterface.Tests.Services.UI;

/// <summary>
/// Overlay-specific layout checks for the join-cancel movie; its view-model bindings are covered
/// by the <see cref="PopupUIMovieBindingTests"/> theory table.
/// </summary>
public class CoopJoinCancelOverlayMovieTests
{
    [Fact]
    public void RootContainer_FillsTheScreenWithoutAcceptingEvents()
    {
        var document = XDocument.Load(PopupUIMovieBindingTests.FindMoviePath("CoopJoinCancelOverlay.xml"));
        var container = document.Descendants("Widget").First();

        Assert.Equal("true", container.Attribute("DoNotAcceptEvents")?.Value);
        Assert.Equal("StretchToParent", container.Attribute("WidthSizePolicy")?.Value);
        Assert.Equal("StretchToParent", container.Attribute("HeightSizePolicy")?.Value);
    }
}
