#if COOP_NAVAL
using Missions.Naval;
using System.Threading;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalShipDamageGateTests
{
    private readonly NavalShipDamageGate gate = new NavalShipDamageGate(new NavalMissionTracker(), new NavalForeignHullRegistry());

    // Q6: each client gives its own agents a ram's morale, so the hull owner's routed apply of the ram hit skips vanilla's.
    [Fact]
    public void RoutedRamApply_SkipsVanillaRamMorale_UntilItsScopeCloses()
    {
        using (gate.RoutedApply())
        {
            using (gate.EnterCollision(isRamDamage: true))
                Assert.False(gate.AllowsRamMorale());
        }

        Assert.True(gate.AllowsRamMorale());
    }

    // A local ram's vanilla collision method gives this client's own agents their morale.
    [Fact]
    public void LocalRam_KeepsVanillaRamMorale()
    {
        using (gate.EnterCollision(isRamDamage: true))
            Assert.True(gate.AllowsRamMorale());
    }

    // A contact collision is queued only on the rammer's owner, so its routed apply keeps vanilla's single morale call.
    [Fact]
    public void RoutedContactCollision_KeepsVanillaRamMorale()
    {
        using (gate.RoutedApply())
        {
            using (gate.EnterCollision(isRamDamage: false))
                Assert.True(gate.AllowsRamMorale());
        }
    }

    // PR #4191 M1: one client's routed apply doesnt reach another client's gate, nor another thread of its own.
    [Fact]
    public void RoutedApply_StaysOnItsGateAndThread()
    {
        var other = new NavalShipDamageGate(new NavalMissionTracker(), new NavalForeignHullRegistry());

        using (gate.RoutedApply())
        {
            using (other.EnterCollision(isRamDamage: true))
                Assert.True(other.AllowsRamMorale());

            bool otherThreadAllows = false;
            var thread = new Thread(() =>
            {
                using (gate.EnterCollision(isRamDamage: true))
                    otherThreadAllows = gate.AllowsRamMorale();
            });
            thread.Start();
            thread.Join();
            Assert.True(otherThreadAllows);
        }
    }
}
#endif
