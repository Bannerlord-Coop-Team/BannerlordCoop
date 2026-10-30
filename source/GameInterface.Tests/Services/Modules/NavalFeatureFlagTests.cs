using GameInterface.Services.Modules;
using Xunit;

namespace GameInterface.Tests.Services.Modules;

/// <summary>
/// Pins which GameInterface builds turn War Sails co-op on: Debug defines TESTER, Release does not.
/// </summary>
public class NavalFeatureFlagTests
{
    // GameInterface.Tests builds against the GameInterface of the same configuration.
    [Fact]
    public void IsEnabled_OnlyInTesterBuilds()
    {
#if DEBUG
        Assert.True(new NavalFeatureFlag().IsEnabled);
#else
        Assert.False(new NavalFeatureFlag().IsEnabled);
#endif
    }
}
