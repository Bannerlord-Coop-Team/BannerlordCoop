#if DEBUG
using GameInterface.Services.MapEvents.NavalScenarios;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents.NavalScenarios;

public class NavalScenarioStagerTests
{
    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, 3, 2)]
    [InlineData(3, 3, 0)]
    [InlineData(5, 3, 0)]
    public void ShipsMissing_TopsUpWithoutRemovingAPlayersShips(int ships, int wanted, int expected)
    {
        Assert.Equal(expected, NavalScenarioStager.ShipsMissing(ships, wanted));
    }

    [Theory]
    [InlineData(0, 3, 3)]
    [InlineData(11, 3, 2)]
    [InlineData(12, 3, 2)]
    [InlineData(22, 3, 1)]
    [InlineData(23, 3, 1)]
    [InlineData(33, 3, 0)]
    [InlineData(80, 3, 0)]
    [InlineData(0, 6, 6)]
    public void TemplatesMissing_AddsWholeTemplatesUntilThePresetTroopCountIsReached(int regulars, int templates, int expected)
    {
        Assert.Equal(expected, NavalScenarioStager.TemplatesMissing(regulars, templates));
    }
}
#endif
