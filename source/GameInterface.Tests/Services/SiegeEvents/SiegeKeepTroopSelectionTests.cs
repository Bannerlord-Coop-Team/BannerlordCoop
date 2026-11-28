using GameInterface.Services.SiegeEvents;
using System.Collections.Generic;
using Xunit;

namespace GameInterface.Tests.Services.SiegeEvents;

public class SiegeKeepTroopSelectionTests
{
    [Fact]
    public void SplitDefenders_FillsTheBudgetFromThePartiesInOrder()
    {
        var budget = new Dictionary<string, int> { ["recruit"] = 5, ["archer"] = 2 };
        var garrison = new Dictionary<string, int> { ["recruit"] = 3, ["archer"] = 4 };
        var lord = new Dictionary<string, int> { ["recruit"] = 4, ["archer"] = 1, ["cavalry"] = 6 };

        var split = SiegeKeepTroopSelection.SplitDefenders(budget,
            new IReadOnlyDictionary<string, int>[] { garrison, lord });

        Assert.Equal(new Dictionary<string, int> { ["recruit"] = 3, ["archer"] = 2 }, split[0]);
        Assert.Equal(new Dictionary<string, int> { ["recruit"] = 2 }, split[1]);
    }
}
