using Xunit;

namespace Common.Tests;

public class ModInformationTests
{
    [Theory]
    [InlineData("NavalDLC", true, true)]
    [InlineData("NavalDLC", false, false)]
    [InlineData("navaldlc", true, false)]
    [InlineData("SandBox", true, false)]
    [InlineData(null, true, false)]
    public void IsNavalDlcModule_MatchesOnlyTheDlcWithTheNavalId(string moduleId, bool isDlc, bool expected)
    {
        Assert.Equal(expected, ModInformation.IsNavalDlcModule(moduleId, isDlc));
    }

    [Theory]
    [InlineData(true, true, "Missions.Naval.dll,Coop.Naval.dll", true)]
    [InlineData(true, true, "Coop.Naval.dll", false)]
    [InlineData(true, true, "Missions.Naval.dll", false)]
    [InlineData(true, true, "", false)]
    [InlineData(false, true, "Missions.Naval.dll,Coop.Naval.dll", false)]
    [InlineData(true, false, "Missions.Naval.dll,Coop.Naval.dll", false)]
    [InlineData(false, false, "Missions.Naval.dll,Coop.Naval.dll", false)]
    [InlineData(true, false, "", false)]
    public void IsNavalSupported_NeedsTheModuleTheFeatureFlagAndBothNavalAssemblies(
        bool navalModuleEnabled, bool navalFeatureEnabled, string shippedFiles, bool expected)
    {
        var shipped = shippedFiles.Split(',');

        Assert.Equal(expected, ModInformation.IsNavalSupported(
            navalModuleEnabled, navalFeatureEnabled, file => System.Array.IndexOf(shipped, file) >= 0));
    }
}
