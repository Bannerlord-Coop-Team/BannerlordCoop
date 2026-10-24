#if COOP_NAVAL
using Missions.Naval;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class RamTickPatchTests
{
    // M10a: the rammer's owner deals the ram once and routes it; a copy's tick would deal it again on every other client.
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void DealsRamHit_OnlyForARamOnAHullThisClientSimulates(bool ownerIsCopy, bool expected)
    {
        Assert.Equal(expected, RamTickPatch.DealsRamHit(ownerIsCopy));
    }
}
#endif
