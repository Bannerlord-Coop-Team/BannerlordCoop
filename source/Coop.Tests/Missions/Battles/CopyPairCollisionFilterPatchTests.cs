#if DEBUG
using Missions.Naval;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class CopyPairCollisionFilterPatchTests
{
    // M10 F5: no client owns the contact of two copies, so only a pair with an own hull may touch.
    [Theory]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, true)]
    public void AllowsCollision_RefusesOnlyAPairOfCopies(bool mineIsCopy, bool otherIsCopy, bool expected)
    {
        Assert.Equal(expected, CopyPairCollisionFilterPatch.AllowsCollision(mineIsCopy, otherIsCopy));
    }
}
#endif
