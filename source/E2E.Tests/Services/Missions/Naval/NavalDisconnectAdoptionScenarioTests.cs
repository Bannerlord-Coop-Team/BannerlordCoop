using GameInterface.Services.MapEvents.TroopSupply;
using Missions.Battles;
using Missions.Messages;
using Newtonsoft.Json.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions.Naval;

/// <summary>
/// M8b: a player who disconnects from a naval battle leaves its hulls behind, and the battle host adopts them as the land
/// host adopts its troops. The host swaps each adopted copy for a simulated hull, streams it at its epoch and crews it from
/// the player's recovered reserve; every other client follows the host's samples. A disconnected battle host's hulls go to
/// its successor with the host migration.
/// </summary>
public class NavalDisconnectAdoptionScenarioTests : NavalBattleTestEnvironment
{
    public NavalDisconnectAdoptionScenarioTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    [Fact]
    public void DisconnectedPlayer_TheHostSimulatesItsHull_ObserversFollowIt_AndTheBattleConcludesOnce()
    {
        using var battle = StartServerBattle(atSea: true, out var concluded, "A", "B", "C");
        var (a, b, c) = (battle["A"], battle["B"], battle["C"]);
        battle.FieldPlayerHull(a, new Vec3(0f, 0f, 0f));
        var hullB = battle.FieldPlayerHull(b, new Vec3(40f, 0f, 0f));
        battle.FieldPlayerHull(c, new Vec3(80f, 0f, 0f));
        var helmsmanId = Guid.NewGuid();
        var helmsman = battle.Peers.ToDictionary(peer => peer, peer => peer.RegisterAgent(helmsmanId, "B"));
        battle.CommitAll();
        battle.TickAll(2);
        b.UseStation(helmsman[b], b.Hull(hullB).Station("helm"), inUse: true);
        b.Hull(hullB).HitPoints = 70f;
        battle.TickAll(2);
        Assert.Same(helmsman[a], a.Hull(hullB).SeatedAt("helm"));
        var copyOnHost = a.Hull(hullB);

        battle.Disconnect(b);

        foreach (var peer in new[] { a, c })
        {
            Assert.True(peer.Has(hullB), $"{peer.Id} dropped the disconnected player's hull");
            Assert.Equal("A", peer.Ship(hullB).CurrentAuthority);
            Assert.True(peer.Ship(hullB).IsAdopted);
        }

        // Park, replace with a simulated hull, board: one step per mission tick, as for an AI hull after a migration.
        a.Ticks(3);
        var simulated = a.Hull(hullB);
        Assert.NotSame(copyOnHost, simulated);
        Assert.True(copyOnHost.IsRemoved);
        Assert.False(simulated.IsCopy);
        Assert.Equal(70f, simulated.HitPoints, 3);
        Assert.Contains(a.Engine.Boardings, boarding =>
            ReferenceEquals(boarding.Hull, simulated.Hull) && boarding.Agents.Any(moved => ReferenceEquals(moved.Agent, helmsman[a])));
        Assert.True(a.Agents.TryGetAgentInfo(helmsmanId, out var crewOnHost));
        Assert.Equal("A", crewOnHost.CurrentAuthority);

        // The host streams it at its epoch, numbered from its own count, and the observer follows it.
        simulated.Frame = new MatrixFrame(Mat3.Identity, new Vec3(55f, 10f, 0f));
        battle.TickAll(2);
        var hostSample = a.SentSamples(hullB).Last();
        Assert.Equal("A", hostSample.OwnerControllerId);
        Assert.Equal(battle.HostEpoch, hostSample.HostEpoch);
        Assert.Equal(hostSample.Sequence, (long)c.Stream(hullB)["accepted"]!);
        Assert.Equal(55f, c.Hull(hullB).Frame.origin.x, 3);
        Assert.True(c.Hull(hullB).IsCopy);

        // The disconnected party's recovered reserve crews its own hull first.
        a.Call(() => a.RecoveredReserve.AddRange(Enumerable.Range(1, 6).Select(seed => new CoopAgentOrigin(
            (CharacterObject)Game.Current.PlayerTroop, null, 0, null, new UniqueTroopDescriptor(seed), battle.PartyIds["B"]))));
        battle.TickAll(2);
        Assert.Equal(6, simulated.ReservedOrigins.Count);
        Assert.Equal(6, (int)c.Stream(hullB)["reserve"]!);

        ResolveOnHost(a, BattleState.AttackerVictory);
        c.Call(() => c.Controller.ResultCommitter.ReportAcceptedResult(), VictoryConclusionDisabledMethods());
        Assert.Equal(1, concluded());
        AssertMapEventRemoved(battle.MapEventId);
    }

    // A disconnect is not a leave: nothing is removed, and the owner's traffic still in flight changes nothing once the host
    // holds the hull.
    [Fact]
    public void DisconnectedPlayer_KeepsItsHullInTheBattle_AndItsLateSampleIsDropped()
    {
        using var battle = StartServerBattle(atSea: true, out _, "A", "B", "C");
        var (a, b, c) = (battle["A"], battle["B"], battle["C"]);
        battle.FieldPlayerHull(a, new Vec3(0f, 0f, 0f));
        var hullB = battle.FieldPlayerHull(b, new Vec3(40f, 0f, 0f));
        battle.CommitAll();
        battle.TickAll(3);
        var lateSample = b.SentSamples(hullB).Last();

        battle.Disconnect(b);
        battle.TickAll(4);
        c.Call(() => c.Broker.Publish(c, lateSample));

        Assert.All(new[] { a, c }, peer =>
        {
            Assert.True(peer.Has(hullB));
            Assert.Equal(0L, (long)JObject.FromObject(peer.Ships.Inspect())["removalsAccepted"]!);
        });
        Assert.Equal("not_authority", (string)c.Stream(hullB)["lastReject"]!);
        Assert.Equal(1L, (long)JObject.FromObject(a.Ships.Inspect())["hullsAdopted"]!);
    }

    // The battle host itself disconnects: its successor adopts the host's own hull with the AI hulls, at the next epoch.
    [Fact]
    public void DisconnectedHost_ItsSuccessorAdoptsTheHostsOwnHull_AndStreamsItAtTheNewEpoch()
    {
        using var battle = StartNavalBattle(new[]
        {
            ("A", BattleSideEnum.Attacker), ("B", BattleSideEnum.Attacker), ("C", BattleSideEnum.Attacker),
        });
        var (a, b, c) = (battle["A"], battle["B"], battle["C"]);
        var hullA = battle.FieldPlayerHull(a, new Vec3(0f, 0f, 0f));
        var pirate = battle.FieldAiHull(a, BattleSideEnum.Defender, new Vec3(0f, 300f, 0f));
        battle.CommitAll();
        battle.TickAll(2);

        battle.MigrateHost(a, "B");

        Assert.All(new[] { b, c }, peer =>
        {
            Assert.Equal("B", peer.Ship(hullA).CurrentAuthority);
            Assert.True(peer.Ship(hullA).IsAdopted);
            Assert.Equal("B", peer.Ship(pirate).CurrentAuthority);
        });
        b.Ticks(3);
        Assert.False(b.Hull(hullA).IsCopy);

        b.Hull(hullA).Frame = new MatrixFrame(Mat3.Identity, new Vec3(-20f, 5f, 0f));
        b.Tick();
        c.Tick();
        Assert.Equal(battle.HostEpoch, b.SentSamples(hullA).Last().HostEpoch);
        Assert.Equal(-20f, c.Hull(hullA).Frame.origin.x, 3);
    }
}
