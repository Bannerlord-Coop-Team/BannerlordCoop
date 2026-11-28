#if COOP_NAVAL
using Coop.Naval.AdvancedStarts;
using GameInterface.Configuration;
using System.Linq;
using Xunit;

namespace Coop.Tests.Naval;

public class NavalAdvancedStartTypeTests
{
    [Fact]
    public void IsOffered_DropsOnlySwitchedOffStartTypes()
    {
        // MerchantVenturer is left unset, which offers it.
        var config = new AdvancedStartConfigData { StartTypes = new AdvancedStartTypesConfigData { FleetAdmiral = false } };

        Assert.Equal(new[] { "merchantventurer" },
            NavalAdvancedStartType.All.Where(startType => startType.IsOffered(config)).Select(startType => startType.Id));
    }
}
#endif
