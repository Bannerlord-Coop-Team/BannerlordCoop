using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions.Naval;

/// <summary>
/// An allied AI fleet at sea: the battle host fields an AI party's hull on its own side, which must go on the side's ally team
/// on every client, not on the host's player team, and stay there when the hull moves to a new host.
/// </summary>
public class NavalAlliedFleetScenarioTests : NavalBattleTestEnvironment
{
    public NavalAlliedFleetScenarioTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    [Fact]
    public void AlliedAiHull_IsOnTheAllyTeamOfTheHostAndEveryPeer_AndStaysThereAfterAHostChange()
    {
        using var battle = StartNavalBattle(new[]
        {
            ("A", BattleSideEnum.Attacker), ("B", BattleSideEnum.Attacker), ("C", BattleSideEnum.Attacker),
        });
        var a = battle["A"];
        var b = battle["B"];
        var c = battle["C"];
        battle.FieldPlayerHull(a, new Vec3(0f, 0f, 0f));
        battle.FieldPlayerHull(b, new Vec3(40f, 0f, 0f));
        battle.FieldPlayerHull(c, new Vec3(80f, 0f, 0f));
        battle.CommitAll();
        battle.TickAll(3);

        // The host's ally team already holds B's and C's copies, so the allied hull takes the next free formation there.
        int freeFormation = 0;
        a.Call(() => freeFormation = a.Engine.FirstFreeFormation(a.Mission.AttackerAllyTeam.Shell));
        var allied = battle.FieldAiHull(a, BattleSideEnum.Attacker, new Vec3(40f, 80f, 0f), freeFormation);
        battle.TickAll(3);

        Assert.True(a.Ship(allied).IsNpcParty);
        Assert.False(a.Hull(allied).IsCopy);
        foreach (var peer in battle.Peers)
        {
            Assert.Same(peer.Mission.AttackerAllyTeam.Shell, peer.Hull(allied).Team);
            Assert.Equal("A", peer.Ship(allied).CurrentAuthority);
        }

        battle.MigrateHost(a, "B");
        battle.TickAll(3);

        Assert.False(b.Hull(allied).IsCopy);
        Assert.True(c.Hull(allied).IsCopy);
        foreach (var peer in new[] { b, c })
        {
            Assert.Same(peer.Mission.AttackerAllyTeam.Shell, peer.Hull(allied).Team);
            Assert.Equal("B", peer.Ship(allied).CurrentAuthority);
        }
    }
}
