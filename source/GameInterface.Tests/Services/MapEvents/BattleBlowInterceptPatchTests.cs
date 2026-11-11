using GameInterface.Services.MapEvents.Patches;
using Xunit;

namespace GameInterface.Tests.Services.MapEvents;

public class BattleBlowInterceptPatchTests
{
    // R43: a non-owner drowned a puppet and routed the self-blow, so the owner's agent took every drowning twice.
    [Fact]
    public void RoutesPuppetBlow_DropsABlowThePuppetDealtItself()
    {
        Assert.False(BattleBlowInterceptPatch.RoutesPuppetBlow(victimIndex: 7, blowOwnerId: 7));
    }

    // A local troop's hit or an ownerless world hit on a puppet still goes to the owner.
    [Theory]
    [InlineData(7, 3)]
    [InlineData(7, -1)]
    public void RoutesPuppetBlow_RoutesAnotherAgentsOrAnOwnerlessBlow(int victimIndex, int blowOwnerId)
    {
        Assert.True(BattleBlowInterceptPatch.RoutesPuppetBlow(victimIndex, blowOwnerId));
    }
}
