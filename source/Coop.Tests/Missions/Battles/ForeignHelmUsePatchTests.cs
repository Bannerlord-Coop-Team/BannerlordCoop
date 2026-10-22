#if DEBUG
using Missions.Naval;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>Which helms the main agent may take in a co-op naval battle.</summary>
public class ForeignHelmUsePatchTests
{
    // The main agent may take only an own-side helm it simulates: never a copy's, and never an enemy hull's before capture replicates.
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    public void AllowsStart_StopsThePlayerTakingAForeignOrEnemyHelm(bool isForeignHelm, bool isEnemyHull, bool expected)
    {
        Assert.Equal(expected, ForeignHelmUsePatch.AllowsStart(isForeignHelm, isEnemyHull));
    }
}
#endif
