using GameInterface.Services.MapEvents.Messages;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions.Naval;

/// <summary>
/// K6/K8: crew membership follows the hull's ship id. Each client fields a copy into its first free formation, so the
/// owner's formation index names another hull, or none, on its peers. Formation updates place crew on the local formation
/// of their hull, also after the AI hulls move to a new host, and a swimmer the owner moves to another hull follows it.
/// </summary>
public class NavalCrewMembershipScenarioTests : NavalBattleTestEnvironment
{
    public NavalCrewMembershipScenarioTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    [Fact]
    public void CrewFollowsItsHull_ThroughTheOwnersUpdates_BeforeAndAfterAMigration()
    {
        using var battle = StartNavalBattle(new[]
        {
            ("A", BattleSideEnum.Attacker), ("B", BattleSideEnum.Attacker), ("C", BattleSideEnum.Attacker),
        });
        var a = battle["A"];
        var b = battle["B"];
        var c = battle["C"];
        // The host's AI fleet took formations 2 and 3; every peer's copies take the first free ones, 0 and 1.
        var p0 = battle.FieldAiHull(a, BattleSideEnum.Defender, new Vec3(0f, 300f, 0f), formationIndex: 2);
        var p1 = battle.FieldAiHull(a, BattleSideEnum.Defender, new Vec3(40f, 300f, 0f), formationIndex: 3);
        battle.CommitAll();
        battle.TickAll();
        Assert.Equal(3, (int)a.Ship(p1).Formation.FormationIndex);
        Assert.All(new[] { b, c }, peer => Assert.Equal(1, (int)peer.Ship(p1).Formation.FormationIndex));

        var crewId = Guid.NewGuid();
        var crew = battle.Peers.ToDictionary(peer => peer.Id, peer => peer.RegisterAgent(crewId, "A", peer.Mission.DefenderTeam));

        // The host's crew member joins p1, which is formation 3 there and formation 1 on its peers.
        JoinHull(a, crew["A"], p1);
        Assert.All(new[] { b, c }, peer => Assert.Same(peer.Ship(p1).Formation, crew[peer.Id].Formation));

        battle.MigrateHost(a, "B");
        b.Ticks(3);
        Assert.False(b.Hull(p1).IsCopy);
        foreach (var peer in battle.Peers)
            peer.Call(() => peer.Agents.TryTransferAuthority("B", crewId, 1));
        Assert.Same(c.Ship(p1).Formation, crew["C"].Formation);

        // On the new host the crew member swims to p0 (vanilla CheckAgentOffShip's TransferAgentToShip), and the observer follows.
        JoinHull(b, crew["B"], p0);
        Assert.Same(c.Ship(p0).Formation, crew["C"].Formation);
        Assert.Same(b.Ship(p0).Formation, crew["B"].Formation);
    }

    // The owner moves its agent onto the hull's formation, as NavalShipAgents.AddAgent does, and flushes the update.
    private static void JoinHull(NavalPeer owner, Agent agent, Guid shipId)
    {
        owner.Call(() =>
        {
            agent.Formation = owner.Ship(shipId).Formation;
            owner.Broker.Publish(owner, new BattleAgentFormationChanged(agent));
        });
        owner.TickController();
    }
}
