using Missions.Messages;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
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

    // K15: a hull's condition, reserve and weapon state go out only on change, so a joiner gets each owner's last ones on entry
    // and applies them once the copy spawns, with the owner's current seats.
    [Fact]
    public void LateJoiner_ReceivesTheCurrentConditionWeaponAndSeatsOfEveryHull()
    {
        using var battle = StartRunningBattle(out var a, out var hulls, out var crewId);
        a.Hull(hulls[2]).FireHitPoints = 5f;
        a.Hull(hulls[2]).SinkingState = 1;
        a.Hull(hulls[0]).SpawnableReserve = 7;
        a.Hull(hulls[1]).ArmWeapon();
        a.Hull(hulls[1]).WeaponState = new BattleShipWeaponState(0.4f, 0.1f, (int)RangedSiegeWeapon.WeaponState.Reloading, 2);
        battle.TickAll(3);

        var c = battle.Join("C");
        c.Engine.ArmsCopies = true;
        var crewOnC = c.RegisterAgent(crewId, "A");
        c.Commit();
        battle.AnnounceEntry(c);
        battle.TickAll(2);

        Assert.Equal(5f, c.Hull(hulls[2]).FireHitPoints, 3);
        Assert.Equal(60f, c.Hull(hulls[0]).HitPoints, 3);
        Assert.Equal(1, c.Hull(hulls[2]).SinkingState);
        Assert.Equal(7, (int)c.Stream(hulls[0])["reserve"]!);
        Assert.Same(crewOnC, c.Hull(hulls[0]).SeatedAt("helm"));
        var weapon = c.Hull(hulls[1]).WeaponState;
        Assert.Equal((int)RangedSiegeWeapon.WeaponState.Reloading, weapon.WeaponState);
        Assert.Equal(2, weapon.AmmoCount);
        Assert.Equal(0.4f, weapon.Direction);
    }

    // D4: a disconnected player's hull left the battle with it, so a joiner that enters afterwards gets only the hulls still
    // in it, and B's record arriving late spawns nothing.
    [Fact]
    public void LateJoiner_AfterADisconnect_GetsOnlyTheHullsStillInTheBattle()
    {
        using var battle = StartRunningBattle(out var a, out var hulls, out _);
        var b = battle["B"];
        var hullB = hulls[^1];
        var lateRecord = b.Network.NetworkSentMessages.GetMessages<NetworkSpawnBattleShips>().Last();
        battle.Disconnect(b);
        battle.TickAll(4);

        var c = battle.Join("C");
        c.Commit();
        battle.AnnounceEntry(c);
        c.Call(() => c.Broker.Publish(c, lateRecord));
        battle.TickAll(3);

        Assert.False(a.Has(hullB));
        Assert.False(c.Has(hullB));
        Assert.Equal(hulls.Take(hulls.Length - 1).OrderBy(id => id), c.Registry.Ships.Select(ship => ship.ShipId).OrderBy(id => id));
        Assert.All(c.Registry.Ships, ship => Assert.Equal("A", ship.CurrentAuthority));
    }

    // R41a-2 E: a member's catch-up reaches the joiner relayed past the server's join hold, so B's own hull record can arrive
    // before C knows B is a member. C holds it and spawns it once B's membership arrives.
    [Fact]
    public void LateJoiner_ThatLearnsAnOwnersMembershipLate_HoldsItsHullUntilThen()
    {
        using var battle = StartRunningBattle(out _, out var hulls, out _);
        var hullB = hulls[^1];

        var c = battle.Join("C");
        c.Members.Forget("B");
        c.Commit();
        battle.AnnounceEntry(c);
        battle.TickAll(2);
        bool spawnedBeforeMembership = c.Has(hullB);
        bool aiHullsSpawned = hulls.Take(hulls.Length - 1).All(c.Has);
        c.Members.Enter("B");
        battle.TickAll(2);

        Assert.False(spawnedBeforeMembership);
        Assert.True(aiHullsSpawned);
        Assert.True(c.Has(hullB));
        Assert.Equal("B", c.Ship(hullB).CurrentAuthority);
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
