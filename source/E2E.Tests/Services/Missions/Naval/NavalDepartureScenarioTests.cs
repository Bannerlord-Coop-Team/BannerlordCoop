using E2E.Tests.Environment.MockEngine;
using Missions.Battles;
using Missions.Messages;
using Newtonsoft.Json.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions.Naval;

/// <summary>
/// M8a: a player retreats from or leaves a naval battle, and its hulls and crew leave with it on every client. Seats on a
/// departing hull are released before it goes, an ally standing on its deck ends on its own hull, its own crew is off the
/// deck when it is removed, nothing the departed owner sent late brings it back, and the battle still concludes once.
/// </summary>
public class NavalDepartureScenarioTests : NavalBattleTestEnvironment
{
    public NavalDepartureScenarioTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    // The retreat preset: B presses Tab and Retreat, its mission ends and removes its hull with the removal sent, then the
    // server tells A and C that B left.
    [Fact]
    public void RetreatingPlayer_TakesItsHullOffEveryClient_AndTheBattleStillConcludesOnce()
    {
        using var battle = StartServerBattle(atSea: true, out var concluded, "A", "B", "C");
        var (a, b, c) = (battle["A"], battle["B"], battle["C"]);
        var hullA = battle.FieldPlayerHull(a, new Vec3(0f, 0f, 0f));
        var hullB = battle.FieldPlayerHull(b, new Vec3(40f, 0f, 0f));
        var hullC = battle.FieldPlayerHull(c, new Vec3(80f, 0f, 0f));
        battle.FieldAiHull(a, BattleSideEnum.Defender, new Vec3(0f, 300f, 0f));
        var helmsman = RegisterEverywhere(battle, Guid.NewGuid(), "B");
        var ally = RegisterEverywhere(battle, Guid.NewGuid(), "A");
        battle.CommitAll();
        battle.TickAll(2);
        SeatAndBoard(battle, b, hullB, helmsman, ally, hullA);

        Retreat(battle, b);
        battle.TickAll(2);

        foreach (var peer in new[] { a, c })
        {
            AssertRemovedWithClearDeck(peer, hullB, ally[peer], hullA);
            Assert.Null(peer.Engine.DeckOf(helmsman[peer]));
            Assert.True(peer.Has(hullA) && peer.Has(hullC));
        }
        Assert.Equal(1, b.Network.NetworkSentMessages.GetMessages<NetworkShipRemoved>()
            .Count(removal => removal.ShipId == hullB && removal.Reason == ShipRemovalReason.Retreat));
        Assert.Equal(1, a.Ships.DepartedHulls(BattleSideEnum.Attacker));

        ResolveOnHost(a, BattleState.AttackerVictory);
        c.Call(() => c.Controller.ResultCommitter.ReportAcceptedResult(), VictoryConclusionDisabledMethods());
        Assert.Equal(1, concluded());
        AssertMapEventRemoved(battle.MapEventId);
    }

    // B leaves and its own removal never reaches the others, as when its mesh stops first: the server's departure removes
    // the hull, and everything B sent that is still in flight changes nothing afterwards.
    [Fact]
    public void LeavingPlayer_WhoseRemovalIsLost_LosesItsHullThroughItsDeparture_AndItsLateTrafficIsDropped()
    {
        using var battle = StartServerBattle(atSea: true, out _, "A", "B", "C");
        var (a, b, c) = (battle["A"], battle["B"], battle["C"]);
        var hullA = battle.FieldPlayerHull(a, new Vec3(0f, 0f, 0f));
        var hullB = battle.FieldPlayerHull(b, new Vec3(40f, 0f, 0f));
        battle.FieldPlayerHull(c, new Vec3(80f, 0f, 0f));
        var helmsman = RegisterEverywhere(battle, Guid.NewGuid(), "B");
        var ally = RegisterEverywhere(battle, Guid.NewGuid(), "A");
        // B's late crew member reached only B, so its station use waits on A and C.
        var lateCrew = b.RegisterAgent(Guid.NewGuid(), "B");
        battle.CommitAll();
        battle.TickAll(2);
        SeatAndBoard(battle, b, hullB, helmsman, ally, hullA);
        b.UseStation(lateCrew, b.Hull(hullB).Station("oar", 1), inUse: true);
        Assert.Equal(1, (int)c.StationStats()["pending"]!);
        var lateSample = b.SentSamples(hullB).Last();
        var lateRecord = b.Network.NetworkSentMessages.GetMessages<NetworkSpawnBattleShips>().Last();

        battle.Leave(b);
        battle.TickAll(2);
        c.Call(() =>
        {
            c.Broker.Publish(c, lateSample);
            c.Broker.Publish(c, lateRecord);
        });
        battle.TickAll(2);

        foreach (var peer in new[] { a, c })
        {
            AssertRemovedWithClearDeck(peer, hullB, ally[peer], hullA);
            Assert.Null(peer.Engine.DeckOf(helmsman[peer]));
            Assert.Equal(0, (int)peer.StationStats()["pending"]!);
            var replicator = JObject.FromObject(peer.Ships.Inspect());
            Assert.Equal(0, (int)replicator["pendingForeignHulls"]!);
            Assert.Equal(0, (int)replicator["heldSamples"]!);
        }
        Assert.Single(c.Engine.AllHulls, hull => hull.SpawnData?.ShipId == hullB);
    }

    // A per-ship Retreat order: B's hull sails off the map and vanilla removes it on B, which tells the others. B stays in the
    // battle with its other hull, which keeps streaming.
    [Fact]
    public void HullRetreatedMidBattle_LeavesEveryClient_AndItsOwnersOtherHullStays()
    {
        using var battle = StartServerBattle(atSea: true, out var concluded, "A", "B", "C");
        var (a, b, c) = (battle["A"], battle["B"], battle["C"]);
        var hullA = battle.FieldPlayerHull(a, new Vec3(0f, 0f, 0f));
        var retreating = battle.FieldPlayerHull(b, new Vec3(40f, 0f, 0f));
        var staying = battle.FieldPlayerHull(b, new Vec3(60f, 0f, 0f));
        var ally = RegisterEverywhere(battle, Guid.NewGuid(), "A");
        battle.CommitAll();
        battle.TickAll(2);
        foreach (var peer in battle.Peers)
            peer.Engine.PlaceAgent(ally[peer], peer.Ship(retreating).Hull, peer.Ship(hullA).Hull);

        b.Call(() =>
        {
            var hull = b.Ship(retreating).Hull;
            b.Ships.AnnounceRemoval(hull, ShipRemovalReason.Retreat, missionEnding: false);
            b.Engine.RemoveHull(hull);
        });
        battle.TickAll(2);
        long streamedBefore = (long)c.Stream(staying)["accepted"]!;
        battle.TickAll(2);

        foreach (var peer in new[] { a, c })
            AssertRemovedWithClearDeck(peer, retreating, ally[peer], hullA);
        Assert.Same(b.Ship(hullA).Hull, b.Engine.DeckOf(ally[b])!.Hull);
        Assert.All(battle.Peers, peer => Assert.True(peer.Has(staying)));
        Assert.True((long)c.Stream(staying)["accepted"]! > streamedBefore);
        Assert.All(battle.Peers, peer => Assert.Equal(1, peer.Ships.DepartedHulls(BattleSideEnum.Attacker)));
        Assert.Equal(0, concluded());
    }

    private static Dictionary<NavalPeer, Agent> RegisterEverywhere(NavalBattle battle, Guid agentId, string authority) =>
        battle.Peers.ToDictionary(peer => peer, peer => peer.RegisterAgent(agentId, authority));

    // B's helmsman, crew of B's hull, takes the helm, which seats its puppet on A and C, and A's ally boards B's deck on every
    // client.
    private static void SeatAndBoard(NavalBattle battle, NavalPeer owner, Guid hull, Dictionary<NavalPeer, Agent> helmsman,
        Dictionary<NavalPeer, Agent> ally, Guid allyHull)
    {
        owner.UseStation(helmsman[owner], owner.Hull(hull).Station("helm"), inUse: true);
        foreach (var peer in battle.Peers)
        {
            peer.Engine.PlaceAgent(helmsman[peer], peer.Ship(hull).Hull, peer.Ship(hull).Hull);
            peer.Engine.PlaceAgent(ally[peer], peer.Ship(hull).Hull, peer.Ship(allyHull).Hull);
            if (peer != owner) Assert.Same(helmsman[peer], peer.Hull(hull).SeatedAt("helm"));
        }
    }

    // Vanilla's retreat on the leaver: OnRetreatMission, then NavalShipsLogic.OnEndMission sends the final ropes and removes
    // every hull, then the mission leave reaches the server, which tells every other member.
    private static void Retreat(NavalBattle battle, NavalPeer leaver)
    {
        leaver.Call(() =>
        {
            leaver.Ships.SendFinalRopes();
            foreach (var ship in leaver.Registry.Ships.ToList())
            {
                leaver.Ships.AnnounceRemoval(ship.Hull, ShipRemovalReason.Retreat, missionEnding: true);
                leaver.Engine.RemoveHull(ship.Hull);
            }
        });
        battle.Leave(leaver);
    }

    private static void AssertRemovedWithClearDeck(NavalPeer peer, Guid departed, Agent ally, Guid allyHull)
    {
        ShipLifecycle state = ShipLifecycle.Unknown;
        peer.Call(() => state = peer.Registry.StateOf(departed));
        Assert.Equal(ShipLifecycle.Removed, state);

        var copy = peer.Engine.AllHulls.Single(hull => hull.SpawnData?.ShipId == departed);
        Assert.True(copy.IsRemoved, $"{peer.Id} kept the copy of {departed}");
        Assert.Equal(0, copy.SeatsAtRemoval);
        Assert.Equal(0, copy.AgentsAboardAtRemoval);
        Assert.Empty(copy.Seats);
        Assert.Same(peer.Ship(allyHull).Hull, peer.Engine.DeckOf(ally)!.Hull);
    }
}
