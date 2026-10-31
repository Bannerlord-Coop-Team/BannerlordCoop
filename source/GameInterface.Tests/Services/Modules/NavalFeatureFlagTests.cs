using GameInterface.Services.Modules;
using System.Linq;
using System.Reflection;
using Xunit;

namespace GameInterface.Tests.Services.Modules;

/// <summary>
/// Pins the flag to the TESTER constant and the CoopNavalDlc property, which this test project gets the same way GameInterface does.
/// </summary>
public class NavalFeatureFlagTests
{
    // A nightly-style build passes TESTER to every project, so both sides see the same constant.
    [Fact]
    public void IsEnabled_MatchesTheTesterConstant()
    {
#if TESTER
        Assert.True(new NavalFeatureFlag().IsEnabled);
#else
        Assert.False(new NavalFeatureFlag().IsEnabled);
#endif
    }

    // Both projects read CoopNavalDlc from Directory.Build.props, so the flag must report what this build compiled.
    [Fact]
    public void IsNavalCodeBuilt_MatchesTheCoopNavalDlcProperty()
    {
        bool builtWithNaval = typeof(NavalFeatureFlagTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Any(attribute => attribute.Key == "CoopNavalDlc" && attribute.Value == "true");

        Assert.Equal(builtWithNaval, new NavalFeatureFlag().IsNavalCodeBuilt);
    }
}
