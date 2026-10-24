using Missions.Battles;
using TaleWorlds.Core;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalPlayerHeroGraceTests
{
    // An AI side has no player hero, so its hulls out of action defeat it at once, as vanilla's enemy side.
    [Fact]
    public void ASideWithNoLivePlayerHeroSeen_HasNoGrace()
    {
        var grace = new NavalPlayerHeroGrace();

        Assert.True(grace.HasExpired(BattleSideEnum.Defender, now: 0f));
    }

    [Fact]
    public void ALivePlayerHero_HoldsItsSideUntilItIsDownForTwentySeconds()
    {
        var grace = new NavalPlayerHeroGrace();

        grace.ObserveLiveHero(BattleSideEnum.Attacker, now: 10f);

        Assert.False(grace.HasExpired(BattleSideEnum.Attacker, now: 30f));
        Assert.True(grace.HasExpired(BattleSideEnum.Attacker, now: 30.5f));
    }

    // PvP: the battle host's side and the other player's side are each held by their own player heroes.
    [Fact]
    public void EachSidesPlayerHeroes_HoldOnlyTheirOwnSide()
    {
        var grace = new NavalPlayerHeroGrace();

        grace.ObserveLiveHero(BattleSideEnum.Attacker, now: 0f);
        grace.ObserveLiveHero(BattleSideEnum.Defender, now: 15f);

        Assert.True(grace.HasExpired(BattleSideEnum.Attacker, now: 25f));
        Assert.False(grace.HasExpired(BattleSideEnum.Defender, now: 25f));
    }
}
