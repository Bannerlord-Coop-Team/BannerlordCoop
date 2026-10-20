using E2E.Tests.Environment.MockEngine;
using Missions.Messages;
using Newtonsoft.Json.Linq;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions.Naval;

/// <summary>
/// Hull damage round trip: a non-owner's hit goes to the hull's owner, which applies it and sends the resulting condition
/// back to every client. Hits from a superseded host epoch are dropped, and a sunk hull stops streaming and lets its
/// seated puppets go.
/// </summary>
public class NavalDamageRoundTripScenarioTests : NavalBattleTestEnvironment
{
    public NavalDamageRoundTripScenarioTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    [Fact]
    public void NonOwnerHit_IsAppliedByTheOwner_AndItsConditionReachesEveryClient()
    {
        using var battle = StartPirateBattle(out var a, out var b, out var c, out var pirate);

        b.Hit(pirate, 30f);
        a.Tick();

        Assert.Single(a.Hull(pirate).AppliedDamage);
        Assert.Empty(b.Hull(pirate).AppliedDamage);
        Assert.Empty(c.Hull(pirate).AppliedDamage);
        Assert.All(battle.Peers, peer => Assert.Equal(70f, peer.Hull(pirate).HitPoints, 3));
        Assert.Equal(1, (long)DamageStats(b)["sent"]!);
        Assert.Equal("not_authority", (string)DamageStats(c)["lastDrop"]!);
    }

    [Fact]
    public void HitFromASupersededHostEpoch_IsDropped()
    {
        using var battle = StartPirateBattle(out var a, out var b, out _, out var pirate);

        b.Call(() => b.Network.SendAll(new NetworkApplyShipDamage(pirate, "B", BattleShipDamageKind.Hull, 30f, 0f, Vec3.Zero,
            Guid.Empty, Guid.Empty, false, -1, battle.HostEpoch - 1)));

        Assert.Empty(a.Hull(pirate).AppliedDamage);
        Assert.Equal(FakeNavalShipEngine.FullHitPoints, a.Hull(pirate).HitPoints, 3);
        Assert.Equal("stale_epoch", (string)DamageStats(a)["lastDrop"]!);
    }

    [Fact]
    public void SunkHull_StopsStreaming_AndItsSeatedPuppetsAreReleased()
    {
        using var battle = StartPirateBattle(out var a, out var b, out var c, out var pirate);
        var crewId = Guid.NewGuid();
        var crewOnA = a.RegisterAgent(crewId, "A");
        var crewOnC = c.RegisterAgent(crewId, "A");
        b.RegisterAgent(crewId, "A");
        a.UseStation(crewOnA, a.Hull(pirate).Station("helm"), inUse: true);
        Assert.Same(crewOnC, c.Hull(pirate).SeatedAt("helm"));

        b.Hit(pirate, 150f);
        a.Tick();
        int samplesWhenSunk = a.SentSamples(pirate).Count;
        battle.TickAll(3);

        Assert.Equal(samplesWhenSunk, a.SentSamples(pirate).Count);
        Assert.All(battle.Peers, peer => Assert.Equal(BattleShipCondition.Sunk, peer.Hull(pirate).SinkingState));
        Assert.Null(c.Hull(pirate).SeatedAt("helm"));
        Assert.Equal(1, (long)c.StationStats()["sinkingSafetyNetReleases"]!);
    }

    // M10a: B's ram hits its copy of the AI hull A simulates; only A applies it, with B's hull resolved as the rammer.
    [Fact]
    public void RamFromAnotherOwnersHull_IsAppliedOnceByTheOwner_WithTheRammerResolved_AndReachesEveryClient()
    {
        using var battle = StartPirateBattle(out var a, out var b, out var c, out var pirate, out var rammer);

        b.Ram(pirate, rammer, 30f);
        a.Tick();

        var applied = Assert.Single(a.Hull(pirate).AppliedDamage);
        Assert.Equal(BattleShipDamageKind.Collision, applied.Kind);
        Assert.True(applied.IsRamDamage);
        Assert.Equal(rammer, applied.HitterShipId);
        Assert.Same(a.Ship(rammer).Hull, Assert.Single(a.Hull(pirate).AppliedHitters));
        Assert.Empty(b.Hull(pirate).AppliedDamage);
        Assert.Empty(c.Hull(pirate).AppliedDamage);
        Assert.All(battle.Peers, peer => Assert.Equal(70f, peer.Hull(pirate).HitPoints, 3));
        Assert.Equal("not_authority", (string)DamageStats(c)["lastDrop"]!);
    }

    // M10a: a copy is steered from the owner's body velocity and yaw rate, which ride every sample.
    [Fact]
    public void OwnersBodyMotion_RidesItsSamples_IntoEveryCopy()
    {
        using var battle = StartPirateBattle(out var a, out var b, out var c, out _, out var rammer);
        var motion = new BattleShipMotion(new Vec2(4f, -1f), 0.1f);
        b.Hull(rammer).Motion = motion;

        battle.TickAll(2);

        Assert.All(new[] { a, c }, peer =>
        {
            Assert.Equal(4f, peer.Hull(rammer).Motion.Velocity.x);
            Assert.Equal(-1f, peer.Hull(rammer).Motion.Velocity.y);
            Assert.Equal(0.1f, peer.Hull(rammer).Motion.YawRate);
        });
    }

    private NavalBattle StartPirateBattle(out NavalPeer a, out NavalPeer b, out NavalPeer c, out Guid pirate, out Guid rammer)
    {
        var battle = StartNavalBattle(new[]
        {
            ("A", BattleSideEnum.Attacker), ("B", BattleSideEnum.Attacker), ("C", BattleSideEnum.Attacker),
        }, hostEpoch: 2);
        a = battle["A"];
        b = battle["B"];
        c = battle["C"];
        pirate = battle.FieldAiHull(a, BattleSideEnum.Defender, new Vec3(0f, 300f, 0f));
        rammer = battle.FieldPlayerHull(b, new Vec3(0f, 260f, 0f));
        battle.CommitAll();
        battle.TickAll();
        return battle;
    }

    // vs_pirates for three players at host epoch 2, with one AI hull the battle host A simulates.
    private NavalBattle StartPirateBattle(out NavalPeer a, out NavalPeer b, out NavalPeer c, out Guid pirate)
    {
        var battle = StartNavalBattle(new[]
        {
            ("A", BattleSideEnum.Attacker), ("B", BattleSideEnum.Attacker), ("C", BattleSideEnum.Attacker),
        }, hostEpoch: 2);
        a = battle["A"];
        b = battle["B"];
        c = battle["C"];
        pirate = battle.FieldAiHull(a, BattleSideEnum.Defender, new Vec3(0f, 300f, 0f));
        battle.CommitAll();
        battle.TickAll();
        return battle;
    }

    private static JObject DamageStats(NavalPeer peer)
    {
        JObject stats = null!;
        peer.Call(() => stats = JObject.FromObject(peer.Damage.Inspect()));
        return stats;
    }
}
