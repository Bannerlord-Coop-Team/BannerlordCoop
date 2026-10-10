#if DEBUG
using Missions.Naval;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class ForeignHelmUsePatchTests
{
    [Theory]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    public void AllowsStart_StopsOnlyThePlayerTakingAForeignHelm(bool isMainAgent, bool isForeignHelm, bool expected)
    {
        Assert.Equal(expected, ForeignHelmUsePatch.AllowsStart(isMainAgent, isForeignHelm));
    }
}
#endif
