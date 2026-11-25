using GameInterface.Configuration;
using GameInterface.Services.AdvancedStart;
using System.Linq;
using Xunit;

namespace GameInterface.Tests.Services.AdvancedStart;

public class VanillaAdvancedStartTypeTests
{
    [Fact]
    public void IsOffered_DropsOnlySwitchedOffStartTypes()
    {
        var config = new AdvancedStartConfigData
        {
            StartTypes = new AdvancedStartTypesConfigData { Beggar = false, King = false, Trader = true },
        };

        Assert.Equal(new[] { "outlaw", "trader", "mercenary", "vassal" },
            VanillaAdvancedStartType.All.Where(startType => startType.IsOffered(config)).Select(startType => startType.Id));
    }
}
