using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions.Naval;

/// <summary>The <c>fleet</c> preset: every player fields three hulls, and each client holds every hull exactly once (M4c).</summary>
public class NavalFleetScenarioTests : NavalBattleTestEnvironment
{
    private const int ShipsPerPlayer = 3;

    public NavalFleetScenarioTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    [Fact]
    public void Fleet_EveryOwnersHullsReplicateOnceEach_EvenWhenTheRecordsArriveAgain()
    {
        using var battle = StartNavalBattle(new[]
        {
            ("A", BattleSideEnum.Attacker), ("B", BattleSideEnum.Attacker), ("C", BattleSideEnum.Attacker),
        });
        var hullsByOwner = battle.Peers.ToDictionary(peer => peer.Id, peer => Enumerable.Range(0, ShipsPerPlayer)
            .Select(i => battle.FieldPlayerHull(peer, new Vec3(30f * i, 100f * peer.Id[0], 0f)))
            .ToArray());
        battle.CommitAll();
        battle.TickAll(2);

        // A peer entering again makes every owner send its records once more.
        foreach (var peer in battle.Peers)
            battle.AnnounceEntry(peer);
        battle.TickAll(2);

        var everyHull = hullsByOwner.Values.SelectMany(hulls => hulls).OrderBy(id => id).ToArray();
        foreach (var peer in battle.Peers)
        {
            Assert.Equal(everyHull, peer.Registry.Ships.Select(ship => ship.ShipId).OrderBy(id => id));
            Assert.Equal(everyHull.Length, peer.Engine.Hulls.Count);
            Assert.Equal(everyHull.Length - ShipsPerPlayer, peer.Engine.Copies.Count);
            Assert.Equal(everyHull.Length - ShipsPerPlayer, peer.Engine.ForeignSpawnAttempts);
            foreach (var owner in hullsByOwner)
                Assert.All(owner.Value, shipId => Assert.Equal(owner.Key, peer.Ship(shipId).CurrentAuthority));
        }
    }
}
