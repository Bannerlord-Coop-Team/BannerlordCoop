using GameInterface.Services.Modules;
using Xunit;

namespace GameInterface.Tests.Services.Modules;

/// <summary>
/// Pins the flag to the TESTER constant, which this test project defines in the same configurations as GameInterface.
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
}
