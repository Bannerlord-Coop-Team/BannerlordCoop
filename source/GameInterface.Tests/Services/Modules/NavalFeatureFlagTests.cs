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
            .Any(attribute => attribute.Key == "CoopNavalDlc" && string.Equals(attribute.Value, "true", System.StringComparison.OrdinalIgnoreCase));

        Assert.Equal(builtWithNaval, new NavalFeatureFlag().IsNavalCodeBuilt);
    }

    // MSBuild treats booleans case-insensitively, so -p:CoopNavalDlc=True must count as a naval build.
    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsTrueBuildProperty_IgnoresCase(string value, bool expected)
    {
        Assert.Equal(expected, NavalFeatureFlag.IsTrueBuildProperty(value));
    }
}
