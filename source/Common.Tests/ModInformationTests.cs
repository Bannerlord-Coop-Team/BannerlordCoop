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
    [InlineData(true, "Missions.Naval.dll,Coop.Naval.dll", true)]
    [InlineData(true, "Coop.Naval.dll", false)]
    [InlineData(true, "Missions.Naval.dll", false)]
    [InlineData(true, "", false)]
    [InlineData(false, "Missions.Naval.dll,Coop.Naval.dll", false)]
    public void IsNavalSupported_NeedsTheModuleAndBothNavalAssemblies(bool navalModuleEnabled, string shippedFiles, bool expected)
    {
        var shipped = shippedFiles.Split(',');

        Assert.Equal(expected, ModInformation.IsNavalSupported(navalModuleEnabled, file => System.Array.IndexOf(shipped, file) >= 0));
    }
}
