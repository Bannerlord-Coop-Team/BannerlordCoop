#if DEBUG
using Missions.Naval;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class CopyRamTickPatchTests
{
    // M10a: the rammer's owner deals the ram once and routes it; a copy's tick would deal it again on every other client.
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void RunsRamTick_OnlyForARamOnAHullThisClientSimulates(bool ownerIsCopy, bool expected)
    {
        Assert.Equal(expected, CopyRamTickPatch.RunsRamTick(ownerIsCopy));
    }
}
#endif
