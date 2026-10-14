using Missions.Battles;
using Missions.Messages;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions.Naval;

/// <summary>
/// M9: a naval battle ends on its own, without coop.debug.naval.win, and never soft-locks. The battle host decides from
/// <see cref="NavalSideTally"/> over its hulls, as <c>CoopNavalBattleEndLogic</c> does, and the server concludes once.
/// </summary>
public class NavalNaturalEndScenarioTests : NavalBattleTestEnvironment
{
    public NavalNaturalEndScenarioTests(ITestOutputHelper output) : base(output, numClients: 2) { }

    // K3: the AI fleet failed to field, so the pirate side never fields. It counts as a side whose reserve was abandoned, the
    // host's hold releases, the empty side reads depleted and the battle concludes once; both players can leave.
    [Fact]
    public void FailedAiFleet_ReleasesTheHold_AndTheEmptyPirateSideEndsTheBattleOnce()
    {
        using var battle = StartServerBattle(atSea: true, out var a, out var b, out var concluded);
        battle.FieldPlayerHull(a, new Vec3(0f, 0f, 0f));
        battle.FieldPlayerHull(b, new Vec3(40f, 0f, 0f));
        battle.CommitAll();
        battle.TickAll(2);

        Assert.False(CoopBattleController.ShouldReleaseEndConditionHold(true, attackerFielded: true, defenderFielded: false,
            attackerMissingReserveAccepted: false, defenderMissingReserveAccepted: false));
        Assert.True(CoopBattleController.ShouldReleaseEndConditionHold(true, attackerFielded: true, defenderFielded: false,
            attackerMissingReserveAccepted: false, defenderMissingReserveAccepted: true));
        var pirates = EndTally(a, BattleSideEnum.Defender, liveHumans: 0);
        Assert.Equal(0, pirates.Hulls);
        Assert.True(pirates.IsDepleted);
        Assert.False(EndTally(a, BattleSideEnum.Attacker, liveHumans: 2).IsDepleted);

        ResolveOnHost(a, BattleState.AttackerVictory);

        Assert.Equal(1, concluded());
        Assert.True(b.Controller.ResultCommitter.TryGetResolvedState(out var peerState));
        Assert.Equal(BattleState.AttackerVictory, peerState);
        // Both players leave through the results screen; their leave reports do not conclude it again.
        foreach (var peer in battle.Peers)
            peer.Call(() => peer.Controller.ResultCommitter.ReportAcceptedResult(), VictoryConclusionDisabledMethods());
        Assert.Equal(1, concluded());
        AssertMapEventRemoved(battle.MapEventId);
    }

    // K4: B's hull holds reserve only B tracks. B's samples carry it, so when every agent of the players' side is down at once
    // the host still counts that reserve: B's hull stays in action and the side is not depleted until the reserve is spent.
    [Fact]
    public void MassKillOnAPeerHullWithReserve_DoesNotDepleteTheSide_UntilThatReserveIsSpent()
    {
        using var battle = StartServerBattle(atSea: true, out var a, out var b, out var concluded);
        battle.FieldPlayerHull(a, new Vec3(0f, 0f, 0f));
        var hullB = battle.FieldPlayerHull(b, new Vec3(40f, 0f, 0f));
        battle.FieldAiHull(a, BattleSideEnum.Defender, new Vec3(0f, 300f, 0f));
        b.Hull(hullB).SpawnableReserve = 20;
        battle.CommitAll();
        battle.TickAll(2);

        Assert.Equal(20, (int)a.Stream(hullB)["reserve"]!);
        var players = EndTally(a, BattleSideEnum.Attacker, liveHumans: 0);
        Assert.Equal(20, players.Reserve);
        Assert.False(players.IsDepleted);
        Assert.Equal(1, players.HullsOutOfAction);
        Assert.False(players.IsOutOfAction);

        b.Hull(hullB).SpawnableReserve = 2;
        battle.TickAll(2);
        Assert.True(EndTally(a, BattleSideEnum.Attacker, liveHumans: 0).IsOutOfAction);

        b.Hull(hullB).SpawnableReserve = 0;
        battle.TickAll(2);
        Assert.True(EndTally(a, BattleSideEnum.Attacker, liveHumans: 0).IsDepleted);
        Assert.Equal(0, concluded());
    }

    // M9: vanilla latches a depleted side the first frame it reads one. A host frame longer than the sample lifetime, with B's
    // samples late, keeps B's reported reserve, so the players' side with no live human stays undepleted.
    [Fact]
    public void HostFrameHitch_WithOnlyPeerReserve_DoesNotDepleteTheSide()
    {
        using var battle = StartServerBattle(atSea: true, out var a, out var b, out var concluded);
        var hullB = battle.FieldPlayerHull(b, new Vec3(40f, 0f, 0f));
        b.Hull(hullB).SpawnableReserve = 20;
        battle.CommitAll();
        battle.TickAll(2);

        a.Tick(BattleShipReplicator.SampleLifetimeSeconds + 1f);

        var players = EndTally(a, BattleSideEnum.Attacker, liveHumans: 0);
        Assert.False((bool)a.Stream(hullB)["streaming"]!);
        Assert.Equal(20, players.Reserve);
        Assert.False(players.IsDepleted);
        Assert.Equal(0, concluded());
    }

    // K4: a peer's reserve stops counting once the peer leaves the battle.
    [Fact]
    public void PeerReserve_StopsCounting_OnceItsOwnerLeaves()
    {
        using var battle = StartServerBattle(atSea: true, out var a, out var b, out _);
        var hullB = battle.FieldPlayerHull(b, new Vec3(40f, 0f, 0f));
        b.Hull(hullB).SpawnableReserve = 20;
        battle.CommitAll();
        battle.TickAll(2);
        Assert.Equal(20, EndTally(a, BattleSideEnum.Attacker, liveHumans: 0).Reserve);

        b.Call(b.Network.Stop);
        a.Call(() => a.Broker.Publish(a, new MissionPeerLeft(b.Id, battle.MapEventId)));
        a.Ticks(2);

        Assert.Equal(0, EndTally(a, BattleSideEnum.Attacker, liveHumans: 0).Reserve);
        Assert.True(EndTally(a, BattleSideEnum.Attacker, liveHumans: 0).IsDepleted);
    }

    // vs_pirates fought to the end: B's hits sink both AI hulls on the host, every client sees them sunk, the pirate side is
    // out of action although its swimmers live, and the host's result concludes the battle once.
    [Fact]
    public void EveryPirateHullSunk_PutsThePirateSideOutOfAction_AndConcludesOnce()
    {
        using var battle = StartServerBattle(atSea: true, out var a, out var b, out var concluded);
        battle.FieldPlayerHull(a, new Vec3(0f, 0f, 0f));
        battle.FieldPlayerHull(b, new Vec3(40f, 0f, 0f));
        var pirates = new[]
        {
            battle.FieldAiHull(a, BattleSideEnum.Defender, new Vec3(0f, 300f, 0f)),
            battle.FieldAiHull(a, BattleSideEnum.Defender, new Vec3(40f, 300f, 0f)),
        };
        battle.CommitAll();
        battle.TickAll(2);
        var crew = pirates.ToDictionary(pirate => pirate, _ => 12);
        Assert.False(EndTally(a, BattleSideEnum.Defender, liveHumans: 24, crew).IsOutOfAction);

        b.Hit(pirates[0], 150f);
        a.Tick();
        var oneSunk = EndTally(a, BattleSideEnum.Defender, liveHumans: 24, crew);
        Assert.Equal(1, oneSunk.HullsOutOfAction);
        Assert.False(oneSunk.IsOutOfAction);

        b.Hit(pirates[1], 150f);
        battle.TickAll(2);

        Assert.All(battle.Peers, peer => Assert.All(pirates,
            pirate => Assert.Equal(BattleShipCondition.Sunk, peer.Hull(pirate).SinkingState)));
        var allSunk = EndTally(a, BattleSideEnum.Defender, liveHumans: 24, crew);
        Assert.True(allSunk.IsOutOfAction);
        Assert.False(allSunk.IsDepleted);
        Assert.Equal(0, allSunk.CrewAboard);
        Assert.Equal(0, concluded());

        ResolveOnHost(a, BattleState.AttackerVictory);
        b.Call(() => b.Controller.ResultCommitter.ReportAcceptedResult(), VictoryConclusionDisabledMethods());

        Assert.Equal(1, concluded());
        AssertMapEventRemoved(battle.MapEventId);
    }
}
