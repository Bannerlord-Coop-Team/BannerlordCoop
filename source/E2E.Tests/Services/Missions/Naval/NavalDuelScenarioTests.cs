using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions.Naval;

/// <summary>The <c>duel</c> preset: two players on opposite sides see each other's hull on the enemy team and hit it through its owner.</summary>
public class NavalDuelScenarioTests : NavalBattleTestEnvironment
{
    public NavalDuelScenarioTests(ITestOutputHelper output) : base(output, numClients: 2) { }

    [Fact]
    public void Duel_EachSeesTheOthersHullOnTheEnemyTeam_AndDamageRoutesToItsOwner()
    {
        using var battle = StartNavalBattle(new[] { ("A", BattleSideEnum.Attacker), ("B", BattleSideEnum.Defender) });
        var a = battle["A"];
        var b = battle["B"];
        var hullA = battle.FieldPlayerHull(a, new Vec3(0f, 0f, 0f));
        var hullB = battle.FieldPlayerHull(b, new Vec3(0f, 300f, 0f));
        battle.CommitAll();
        battle.TickAll();

        Assert.Same(a.Mission.DefenderTeam.Shell, a.Hull(hullB).Team);
        Assert.Same(b.Mission.AttackerTeam.Shell, b.Hull(hullA).Team);
        Assert.NotSame(a.Mission.PlayerTeam.Shell, a.Hull(hullB).Team);
        Assert.NotSame(b.Mission.PlayerTeam.Shell, b.Hull(hullA).Team);
        Assert.Equal("B", a.Ship(hullB).CurrentAuthority);
        Assert.Equal("A", b.Ship(hullA).CurrentAuthority);

        a.Hit(hullB, 25f);
        b.Hit(hullA, 40f);
        battle.TickAll();

        Assert.Single(b.Hull(hullB).AppliedDamage);
        Assert.Single(a.Hull(hullA).AppliedDamage);
        Assert.Empty(a.Hull(hullB).AppliedDamage);
        Assert.Empty(b.Hull(hullA).AppliedDamage);
        Assert.Equal(75f, a.Hull(hullB).HitPoints, 3);
        Assert.Equal(60f, b.Hull(hullA).HitPoints, 3);
    }
}
