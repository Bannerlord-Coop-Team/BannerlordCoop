#if COOP_NAVAL
using Missions.Naval;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class CoopNavalBattleLauncherTests
{
    [Fact]
    public void OrderOwnCaptains_SeatsTheLeaderFirstAndOneHeroPerShip()
    {
        var captains = CoopNavalBattleLauncher.OrderOwnCaptains(
            new[] { "companion_a", "main_hero", "companion_b" },
            "main_hero",
            shipCount: 2);

        Assert.Equal(new[] { "main_hero", "companion_a" }, captains);
    }
}
#endif
