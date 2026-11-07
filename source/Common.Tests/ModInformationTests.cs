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
}
