using Missions.Messages;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions.Naval;

/// <summary>
/// The <c>vs_pirates</c> preset over the mesh: two players on one side announce a hull each and the battle host fields
/// three AI hulls against them.
/// </summary>
public class NavalVsPiratesScenarioTests : NavalBattleTestEnvironment
{
    public NavalVsPiratesScenarioTests(ITestOutputHelper output) : base(output, numClients: 2) { }

    [Fact]
    public void VsPirates_EveryClientSeesEveryHullOnceWithItsAuthority()
    {
        using var battle = StartNavalBattle(new[] { ("A", BattleSideEnum.Attacker), ("B", BattleSideEnum.Attacker) });
        var a = battle["A"];
        var b = battle["B"];
        var hullA = battle.FieldPlayerHull(a, new Vec3(0f, 0f, 0f));
        var hullB = battle.FieldPlayerHull(b, new Vec3(30f, 0f, 0f));
        var pirates = Enumerable.Range(0, 3)
            .Select(i => battle.FieldAiHull(a, BattleSideEnum.Defender, new Vec3(30f * i, 300f, 0f)))
            .ToArray();

        battle.CommitAll();
        battle.TickAll(3);

        var everyHull = new[] { hullA, hullB }.Concat(pirates).ToArray();
        foreach (var peer in battle.Peers)
        {
            Assert.Equal(everyHull.OrderBy(id => id), peer.Registry.Ships.Select(ship => ship.ShipId).OrderBy(id => id));
            Assert.Equal(everyHull.Length, peer.Engine.Hulls.Count);
            Assert.Equal("A", peer.Ship(hullA).CurrentAuthority);
            Assert.Equal("B", peer.Ship(hullB).CurrentAuthority);
            Assert.All(pirates, pirate => Assert.Equal("A", peer.Ship(pirate).CurrentAuthority));
            Assert.All(pirates, pirate => Assert.True(peer.Ship(pirate).IsNpcParty));
        }

        Assert.Equal(1, a.Engine.ForeignSpawnAttempts);
        Assert.Equal(4, b.Engine.ForeignSpawnAttempts);
        Assert.Same(a.Mission.AttackerAllyTeam.Shell, a.Hull(hullB).Team);
        Assert.Same(b.Mission.AttackerAllyTeam.Shell, b.Hull(hullA).Team);
        Assert.All(pirates, pirate => Assert.Same(b.Mission.DefenderTeam.Shell, b.Hull(pirate).Team));
        Assert.All(pirates, pirate => Assert.False(a.Hull(pirate).IsCopy));
    }

    [Fact]
    public void VsPirates_SamplesStreamInSequenceAndExpireOnTheReceiversClock()
    {
        using var battle = StartNavalBattle(new[] { ("A", BattleSideEnum.Attacker), ("B", BattleSideEnum.Attacker) });
        var a = battle["A"];
        var b = battle["B"];
        var hullA = battle.FieldPlayerHull(a, new Vec3(0f, 0f, 0f));
        battle.FieldPlayerHull(b, new Vec3(30f, 0f, 0f));
        battle.CommitAll();
        battle.TickAll();
        var firstSample = a.SentSamples(hullA).First();

        // The clocks are far apart: the sender's mission runs minutes ahead and the receiver's seconds.
        a.Tick(500f);
        b.Tick(3f);
        a.Hull(hullA).Frame = new MatrixFrame(Mat3.Identity, new Vec3(10f, 0f, 0f));
        a.Tick();
        b.Tick();

        Assert.Equal(10f, b.Hull(hullA).Frame.origin.x, 3);
        Assert.True((bool)b.Stream(hullA)["streaming"]!);
        long latest = a.SentSamples(hullA).Last().Sequence;

        a.Call(() => a.Network.SendAll(firstSample));
        // A received sample applies with the arrivals of the receiver's next tick.
        battle.ApplyArrivals();

        Assert.Equal("stale", (string)b.Stream(hullA)["lastReject"]!);
        Assert.Equal(10f, b.Hull(hullA).Frame.origin.x, 3);
        Assert.Equal(latest, (long)b.Stream(hullA)["accepted"]!);

        b.Tick(0.9f);
        Assert.True((bool)b.Stream(hullA)["streaming"]!);

        b.Tick(0.2f);
        Assert.False((bool)b.Stream(hullA)["streaming"]!);
        Assert.Equal("expired", (string)b.Stream(hullA)["lastReject"]!);
    }
}
