using GameInterface.Services.UI.Encyclopedia;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using Xunit;

namespace GameInterface.Tests.Services.UI;

public class EncyclopediaSettlementWorkshopsTests
{
    [Fact]
    public void SettlementPageViewModel_IsDiscoverableByTheEncyclopediaFactory()
    {
        var pageType = typeof(CoopEncyclopediaSettlementPageVM);

        Assert.True(typeof(EncyclopediaSettlementPageVM).IsAssignableFrom(pageType));
        var attribute = Assert.Single(pageType.GetCustomAttributes(false)
            .OfType<EncyclopediaViewModel>());
        Assert.Equal(typeof(Settlement), attribute.PageTargetType);
        Assert.NotNull(pageType.GetConstructor(new[] { typeof(EncyclopediaPageArgs) }));
    }

    [Fact]
    public void SettlementPageMovie_BindsACompactHorizontalWorkshopList()
    {
        var document = XDocument.Load(FindMoviePath());
        var list = FindById(document, "WorkshopsList");

        Assert.DoesNotContain(document.Descendants(),
            element => element.Attribute("Id")?.Value == "WorkshopsDivider");
        Assert.Equal("{Workshops}", list.Attribute("DataSource")?.Value);
        Assert.Equal("@HasWorkshops", list.Attribute("IsVisible")?.Value);
        Assert.Equal("HorizontalLeftToRight", list.Attribute("StackLayout.LayoutMethod")?.Value);
        Assert.Equal("20", list.Attribute("MarginLeft")?.Value);
        Assert.Equal("10", list.Attribute("MarginTop")?.Value);

        var workshopCard = Assert.Single(list.Descendants("BrushWidget"));
        Assert.Equal("70", workshopCard.Attribute("SuggestedWidth")?.Value);
        Assert.Equal("50", workshopCard.Attribute("SuggestedHeight")?.Value);

        var workshopIcon = Assert.Single(list.Descendants("ShopVisualIconBrushWidget"),
            element => element.Attribute("ShopId")?.Value == "@ShopId");
        Assert.Equal("35", workshopIcon.Attribute("SuggestedWidth")?.Value);
        Assert.Equal("26", workshopIcon.Attribute("SuggestedHeight")?.Value);
        Assert.Single(list.Descendants("HintWidget"),
            element => element.Attribute("Command.HoverBegin")?.Value == "ExecuteBeginHint" &&
                       element.Attribute("Command.HoverEnd")?.Value == "ExecuteEndHint");
    }

    [Fact]
    public void SettlementPageMovie_PlacesWorkshopsBetweenInformationAndOwner()
    {
        var document = XDocument.Load(FindMoviePath());
        var settlementsGrid = FindById(document, "SettlementsGrid");
        var childIds = FindById(document, "RightSideList")
            .Descendants()
            .Select(element => element.Attribute("Id")?.Value ?? element.Attribute("Text")?.Value)
            .Where(id => id == "@InformationText" || id == "WorkshopsList" || id == "OwnerDivider")
            .ToArray();

        Assert.Equal(new[] { "@InformationText", "WorkshopsList", "OwnerDivider" }, childIds);
        Assert.Equal("50", settlementsGrid.Attribute("MarginBottom")?.Value);
    }

    private static XElement FindById(XDocument document, string id)
    {
        return Assert.Single(document.Descendants(),
            element => element.Attribute("Id")?.Value == id);
    }

    private static string FindMoviePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var path = Path.Combine(directory.FullName, "UIMovies", "EncyclopediaSettlementPage.xml");
            if (File.Exists(path)) return path;

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"Unable to find UIMovies/EncyclopediaSettlementPage.xml from {AppContext.BaseDirectory}");
    }
}
