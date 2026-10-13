using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions.Naval;

/// <summary>
/// The <c>late_join</c> preset: a third player enters mid-battle and receives every current hull from its owner, the
/// owners' current seats and their sample streams.
/// </summary>
public class NavalLateJoinScenarioTests : NavalBattleTestEnvironment
{
    public NavalLateJoinScenarioTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    [Fact]
    public void LateJoiner_ReceivesEveryCurrentHullOnce_WithItsOwnersSeatsAndStream()
    {
        using var battle = StartRunningBattle(out var a, out var hulls, out var crewId);

        var c = battle.Join("C");
        var crewOnC = c.RegisterAgent(crewId, "A");
        c.Commit();
        battle.AnnounceEntry(c);
        a.Hull(hulls[0]).Frame = new MatrixFrame(Mat3.Identity, new Vec3(-50f, 300f, 0f));
        battle.TickAll(2);

        Assert.Equal(hulls.OrderBy(id => id), c.Registry.Ships.Select(ship => ship.ShipId).OrderBy(id => id));
        Assert.Equal(hulls.Length, c.Engine.ForeignSpawnAttempts);
        Assert.Equal(hulls.Length, c.Engine.Copies.Count);
        Assert.Equal("B", c.Ship(hulls[^1]).CurrentAuthority);
        Assert.All(hulls.Take(hulls.Length - 1), shipId => Assert.Equal("A", c.Ship(shipId).CurrentAuthority));
        Assert.Same(crewOnC, c.Hull(hulls[0]).SeatedAt("helm"));
        Assert.Equal(-50f, c.Hull(hulls[0]).Frame.origin.x, 3);
    }

    [Fact(Skip = "K15: an owner sends a hull's condition only when it changes, so a joiner misses fire and sinking state set before it entered; no condition catch-up exists yet.")]
    public void LateJoiner_ReceivesTheCurrentConditionOfEveryHull()
    {
        using var battle = StartRunningBattle(out var a, out var hulls, out _);
        a.Hull(hulls[0]).FireHitPoints = 5f;
        a.Hull(hulls[0]).SinkingState = 1;
        battle.TickAll();

        var c = battle.Join("C");
        c.Commit();
        battle.AnnounceEntry(c);
        battle.TickAll(2);

        Assert.Equal(5f, c.Hull(hulls[0]).FireHitPoints, 3);
        Assert.Equal(1, c.Hull(hulls[0]).SinkingState);
    }

    // vs_pirates with A hosting three AI hulls, one of them crewed at the helm and damaged, and B's hull; C still to join.
    private NavalBattle StartRunningBattle(out NavalPeer a, out Guid[] hulls, out Guid crewId)
    {
        var battle = StartNavalBattle(new[]
        {
            ("A", BattleSideEnum.Attacker), ("B", BattleSideEnum.Attacker), ("C", BattleSideEnum.Attacker),
        }, lateJoiners: "C");
        a = battle["A"];
        var host = a;
        var b = battle["B"];
        var pirates = Enumerable.Range(0, 3)
            .Select(i => battle.FieldAiHull(host, BattleSideEnum.Defender, new Vec3(30f * i, 300f, 0f)))
            .ToArray();
        hulls = pirates.Append(battle.FieldPlayerHull(b, new Vec3(30f, 0f, 0f))).ToArray();
        crewId = Guid.NewGuid();
        var crewOnA = a.RegisterAgent(crewId, "A");
        b.RegisterAgent(crewId, "A");
        battle.CommitAll();
        battle.TickAll();
        a.UseStation(crewOnA, a.Hull(pirates[0]).Station("helm"), inUse: true);
        a.Hull(pirates[0]).HitPoints = 60f;
        battle.TickAll(2);
        return battle;
    }
}
