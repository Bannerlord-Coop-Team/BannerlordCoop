#if DEBUG
using Missions.Naval;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>Which helms the main agent may take in a co-op naval battle.</summary>
public class ForeignHelmUsePatchTests
{
    // Any hull this client simulates goes to vanilla's helm rules; another owner's copy only when it is an enemy, to capture it.
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void AllowsStart_OnlyLetsThePlayerOntoAnEnemyCopysHelm(bool isForeignHelm, bool isEnemyHull, bool expected)
    {
        Assert.Equal(expected, ForeignHelmUsePatch.AllowsStart(isForeignHelm, isEnemyHull));
    }
}
#endif
