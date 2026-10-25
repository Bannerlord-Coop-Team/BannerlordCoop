#if COOP_NAVAL
using Missions.Naval;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalShipDamageGateTests
{
    // Q6: each client gives its own agents a ram's morale, so the hull owner's routed apply of the ram hit skips vanilla's.
    [Fact]
    public void RoutedRamApply_SkipsVanillaRamMorale_UntilItsScopeCloses()
    {
        using (NavalShipDamageGate.RoutedApply())
        {
            var outer = NavalShipDamageGate.EnterCollision(isRamDamage: true);
            Assert.False(NavalShipDamageGate.AllowsRamMorale());
            NavalShipDamageGate.ExitCollision(outer);
        }

        Assert.True(NavalShipDamageGate.AllowsRamMorale());
    }

    // A local ram's vanilla collision method gives this client's own agents their morale.
    [Fact]
    public void LocalRam_KeepsVanillaRamMorale()
    {
        var outer = NavalShipDamageGate.EnterCollision(isRamDamage: true);

        Assert.True(NavalShipDamageGate.AllowsRamMorale());
        NavalShipDamageGate.ExitCollision(outer);
    }

    // A contact collision is queued only on the rammer's owner, so its routed apply keeps vanilla's single morale call.
    [Fact]
    public void RoutedContactCollision_KeepsVanillaRamMorale()
    {
        using (NavalShipDamageGate.RoutedApply())
        {
            var outer = NavalShipDamageGate.EnterCollision(isRamDamage: false);
            Assert.True(NavalShipDamageGate.AllowsRamMorale());
            NavalShipDamageGate.ExitCollision(outer);
        }
    }
}
#endif
