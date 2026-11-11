using E2E.Tests.Environment.MockEngine;
using E2E.Tests.Util;
using System.Runtime.CompilerServices;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
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
        // Each owner applies the hit it was sent, and its new condition reaches the other client's copy a tick later.
        battle.TickAll(2);

        Assert.Single(b.Hull(hullB).AppliedDamage);
        Assert.Single(a.Hull(hullA).AppliedDamage);
        Assert.Empty(a.Hull(hullB).AppliedDamage);
        Assert.Empty(b.Hull(hullA).AppliedDamage);
        Assert.Equal(75f, a.Hull(hullB).HitPoints, 3);
        Assert.Equal(60f, b.Hull(hullA).HitPoints, 3);
    }

    // Every spawn raises the spawn event, and only a hull on this client's PlayerTeam is its own: each player's hull registers
    // once under its owner and party, and the copy of the other's hull is known only by its record's id.
    [Fact]
    public void Duel_OwnHullsRegisterFromTheSpawnEvent_AndTheCopiesAddNoOwnHull()
    {
        using var battle = StartNavalBattle(new[] { ("A", BattleSideEnum.Attacker), ("B", BattleSideEnum.Defender) });
        var a = battle["A"];
        var b = battle["B"];
        var hullA = battle.FieldPlayerHull(a, new Vec3(0f, 0f, 0f));
        var hullB = battle.FieldPlayerHull(b, new Vec3(0f, 300f, 0f));
        Assert.Equal(new[] { hullA }, a.RegisteredShipIds());
        Assert.Equal(new[] { hullB }, b.RegisteredShipIds());

        battle.CommitAll();
        battle.TickAll();

        var both = new[] { hullA, hullB }.OrderBy(id => id).ToArray();
        Assert.Equal(both, a.RegisteredShipIds());
        Assert.Equal(both, b.RegisteredShipIds());
        Assert.Equal(("B", battle.PartyIds["B"], false), (a.Ship(hullB).CurrentAuthority, a.Ship(hullB).MapEventPartyId,
            a.Ship(hullB).IsNpcParty));
    }

    // #4213: after B rams A, B's copy of A's oarsman is let go locally, recovers, and is let go again on the next frame, as
    // vanilla's oar tick did live. B keeps re-seating it at a bounded cadence while A keeps the seat, and stops once A releases.
    [Fact]
    public void Duel_ACopysOarsmanLetGoAgainAfterARam_IsReseatedWhileItsOwnerKeepsTheSeat()
    {
        using var battle = StartNavalBattle(new[] { ("A", BattleSideEnum.Attacker), ("B", BattleSideEnum.Defender) });
        var a = battle["A"];
        var b = battle["B"];
        var hullA = battle.FieldPlayerHull(a, new Vec3(0f, 0f, 0f));
        var hullB = battle.FieldPlayerHull(b, new Vec3(0f, 300f, 0f));
        var crewId = Guid.NewGuid();
        var crewOnA = a.RegisterAgent(crewId, "A");
        var crewOnB = b.RegisterAgent(crewId, "A");
        battle.CommitAll();
        battle.TickAll();
        var oarOnA = a.Hull(hullA).Station("oar");
        a.UseStation(crewOnA, oarOnA, inUse: true);
        Assert.Same(crewOnB, b.Hull(hullA).SeatedAt("oar"));

        b.Ram(hullA, hullB, 30f);
        b.Hull(hullA).Seats.Clear();
        b.Tick();
        var recovered = b.Hull(hullA).SeatedAt("oar");
        b.Hull(hullA).Seats.Clear();
        b.Tick();
        var withinInterval = b.Hull(hullA).SeatedAt("oar");
        b.Ticks(20);
        var afterInterval = b.Hull(hullA).SeatedAt("oar");
        battle.TickAll(2);

        Assert.Same(crewOnB, recovered);
        Assert.Null(withinInterval);
        Assert.Same(crewOnB, afterInterval);
        Assert.Single(a.Hull(hullA).AppliedDamage);
        Assert.Equal(1, (long)b.StationStats()["unownedPuppetReleases"]!);

        a.UseStation(crewOnA, oarOnA, inUse: false);
        b.Hull(hullA).Seats.Clear();
        b.Ticks(25);

        Assert.Null(b.Hull(hullA).SeatedAt("oar"));
        Assert.Equal(0, (int)b.StationStats()["appliedSeats"]!);
    }

    // #4213: A's own rower walks in and sits down from deck height while B seats its copy at vanilla's spawn spot; a ram then
    // staggers B's copy without a release. Once each settles, both clients hold it at the same spot of the station.
    [Fact]
    public void Duel_AnOarsmanWalkedInOnItsOwnerAndStaggeredOnItsCopy_SitsAtTheSameSpotOnBothOnceSettled()
    {
        using var battle = StartNavalBattle(new[] { ("A", BattleSideEnum.Attacker), ("B", BattleSideEnum.Defender) });
        var a = battle["A"];
        var b = battle["B"];
        var hullA = battle.FieldPlayerHull(a, new Vec3(0f, 0f, 0f));
        var hullB = battle.FieldPlayerHull(b, new Vec3(0f, 300f, 0f));
        var crewId = Guid.NewGuid();
        var crewOnA = a.RegisterAgent(crewId, "A");
        var crewOnB = b.RegisterAgent(crewId, "A");
        battle.CommitAll();
        battle.TickAll();
        a.Engine.WalksIn.Add(crewOnA);
        var oarOnA = a.Hull(hullA).Station("oar");
        a.UseStation(crewOnA, oarOnA, inUse: true);
        Assert.Same(crewOnB, b.Hull(hullA).SeatedAt("oar"));

        battle.TickAll(3);
        Assert.Empty(a.Engine.SeatPlacements);
        a.Call(() => a.Engine.FinishSitDown(crewOnA));
        battle.TickAll(3);

        b.Ram(hullA, hullB, 30f);
        b.Call(() => b.Engine.Stagger(crewOnB, isStruck: true, new Vec3(0f, 0.04f, 0.055f)));
        battle.TickAll(3);
        Assert.Empty(b.Engine.SeatPlacements);
        b.Call(() => b.Engine.Stagger(crewOnB, isStruck: false, Vec3.Zero));
        battle.TickAll(3);

        var onA = a.Engine.SeatPoses[crewOnA];
        var onB = b.Engine.SeatPoses[crewOnB];
        var spawnOffset = FakeNavalShipEngine.SitDownDisplacement;
        Assert.Single(a.Engine.SeatPlacements);
        Assert.Single(b.Engine.SeatPlacements);
        Assert.True((onA.Position - onA.StationFrame.origin).DistanceSquared(spawnOffset) < 1e-8f, $"owner sits at {onA.Position}");
        Assert.True((onB.Position - onB.StationFrame.origin).DistanceSquared(spawnOffset) < 1e-8f, $"copy sits at {onB.Position}");
        Assert.Same(crewOnA, a.Hull(hullA).SeatedAt("oar"));
        Assert.Same(crewOnB, b.Hull(hullA).SeatedAt("oar"));
    }

    // #4213 PvE: A's own rower walks back to an oar after boarding, while B's copy of it is off the hull. B seats the copy from A's
    // use at the spawn spot; on the next frame the engine puts it on the hull 13 cm higher, where it sat in row idle for good.
    // Both end at the same spot of the oar.
    [Fact]
    public void Duel_ACopySeatedAsItsOwnerWalkedInAndMovedOnTheNextFrame_SitsAtTheSameSpotAsItsOwner()
    {
        using var battle = StartNavalBattle(new[] { ("A", BattleSideEnum.Attacker), ("B", BattleSideEnum.Defender) });
        var a = battle["A"];
        var b = battle["B"];
        var hullA = battle.FieldPlayerHull(a, new Vec3(0f, 0f, 0f));
        battle.FieldPlayerHull(b, new Vec3(0f, 300f, 0f));
        var crewId = Guid.NewGuid();
        var crewOnA = a.RegisterAgent(crewId, "A");
        var crewOnB = b.RegisterAgent(crewId, "A");
        battle.CommitAll();
        battle.TickAll();
        a.Engine.WalksIn.Add(crewOnA);

        // Each of B's ticks is one frame, so the use's frame is the only settle pass before the engine moves the copy.
        a.UseStation(crewOnA, a.Hull(hullA).Station("oar"), inUse: true, settlePeers: false);
        for (int frame = 0; frame < 10 && b.Hull(hullA).SeatedAt("oar") == null; frame++)
            b.Tick();
        Assert.Same(crewOnB, b.Hull(hullA).SeatedAt("oar"));
        b.Call(() => b.Engine.Shift(crewOnB, new Vec3(0f, 0.08f, 0.13f)));
        battle.TickAll(3);
        a.Call(() => a.Engine.FinishSitDown(crewOnA));
        battle.TickAll(3);

        var onA = a.Engine.SeatPoses[crewOnA];
        var onB = b.Engine.SeatPoses[crewOnB];
        var spawnOffset = FakeNavalShipEngine.SitDownDisplacement;
        Assert.Single(a.Engine.SeatPlacements);
        Assert.Single(b.Engine.SeatPlacements);
        Assert.True((onA.Position - onA.StationFrame.origin).DistanceSquared(spawnOffset) < 1e-8f, $"owner sits at {onA.Position}");
        Assert.True((onB.Position - onB.StationFrame.origin).DistanceSquared(spawnOffset) < 1e-8f, $"copy sits at {onB.Position}");
    }

#if COOP_NAVAL
    private const string HookKey = "4:attachment_machine_holder/28:attachment_point_machine_nested/3:pilot";
    private static readonly Vec3 HookOffset = new(-3f, 6f, 2f);

    // A's crew mans a grappling hook, whose seat vanilla only position-locks, and A's hull gets under way. B keeps its copy of
    // the crew walking to the hook on the moved hull, otherwise the copy walks to where the hook was and off the side.
    [Fact]
    public void Duel_ACopysHookmanAsItsHullGetsUnderWay_IsKeptAtTheHook()
    {
        using var battle = StartNavalBattle(new[] { ("A", BattleSideEnum.Attacker), ("B", BattleSideEnum.Defender) });
        var a = battle["A"];
        var b = battle["B"];
        var hullA = battle.FieldPlayerHull(a, new Vec3(0f, 0f, 0f));
        battle.FieldPlayerHull(b, new Vec3(0f, 300f, 0f));
        var crewId = Guid.NewGuid();
        var crewOnA = a.RegisterAgent(crewId, "A");
        var crewOnB = b.RegisterAgent(crewId, "A");
        battle.CommitAll();
        battle.TickAll();
        var copyOfA = b.Hull(hullA);
        var hookOnB = MissionTestObjects.CreateStandingPoint<HookStandingPoint>();
        hookOnB.Hull = copyOfA;
        // As ShipAttachmentMachine.OnDeploymentFinished leaves a hook's pilot seat.
        hookOnB.LockUserFrames = false;
        hookOnB.LockUserPositions = true;
        copyOfA.Stations[(HookKey, 0)] = hookOnB;
        b.Engine.Pin = ((global::Missions.Naval.NavalShipEngine)RuntimeHelpers.GetUninitializedObject(
            typeof(global::Missions.Naval.NavalShipEngine))).PinToStation;

        a.UseStation(crewOnA, a.Hull(hullA).Station(HookKey), inUse: true);
        Assert.Same(crewOnB, copyOfA.SeatedAt(HookKey));
        a.Hull(hullA).Frame = new MatrixFrame(Mat3.Identity, new Vec3(20f, 0f, 0f));
        battle.TickAll(3);

        Assert.Equal(20f, copyOfA.Frame.origin.x, 3);
        Assert.True(AgentMirror.TryGet(crewOnB, out var copy));
        var hook = copyOfA.Frame.TransformToParent(HookOffset).AsVec2;
        Assert.True(copy.LastTargetPosition.DistanceSquared(hook) < 1e-6f,
            $"B's copy walks to {copy.LastTargetPosition}, the hook is at {hook}");
        Assert.Same(crewOnB, copyOfA.SeatedAt(HookKey));
    }

    /// <summary>A hook's pilot seat on a fake hull: its user frame rides the hull's current frame, as a prefab point's does.</summary>
    private sealed class HookStandingPoint : StandingPoint
    {
        public FakeHull Hull { get; set; } = null!;

        // The seat at its offset on the hull's current frame.
        public override WorldFrame GetUserFrameForAgent(Agent agent) => new(Hull.Frame.rotation,
            new WorldPosition(UIntPtr.Zero, Hull.Frame.TransformToParent(HookOffset)));
    }
#endif
}
