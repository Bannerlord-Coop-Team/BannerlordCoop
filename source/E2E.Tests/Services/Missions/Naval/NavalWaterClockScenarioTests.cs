using E2E.Tests.Environment.MockEngine;
using TaleWorlds.Core;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions.Naval;

/// <summary>
/// Water time across a host change: every client writes the battle host's water time over an in-memory water clock, a
/// promoted host that never heard the old one catches up to a client's report, and no client's waves run backwards.
/// </summary>
public class NavalWaterClockScenarioTests : NavalBattleTestEnvironment
{
    public NavalWaterClockScenarioTests(ITestOutputHelper output) : base(output, numClients: 3) { }

    [Fact]
    public void HostChange_TheLaggingNewHostCatchesUpAndNoClientsWaterRunsBackwards()
    {
        using var battle = StartNavalBattle(new[]
        {
            ("A", BattleSideEnum.Attacker), ("B", BattleSideEnum.Attacker), ("C", BattleSideEnum.Attacker),
        });
        var a = battle["A"];
        var b = battle["B"];
        var c = battle["C"];
        var waterA = WaterOf(a);
        var waterB = WaterOf(b);
        var waterC = WaterOf(c);
        // Different mission histories, so each client's vanilla clock is far from the others'.
        waterA.Installed = true;
        waterA.Vanilla = 5000f;
        waterC.Installed = true;
        waterC.Vanilla = 300f;

        // 30 s with A hosting; B's clock comes up only once A has stopped sending, so B never hears A's time.
        for (int tick = 0; tick < 600; tick++)
        {
            Advance(a);
            Advance(c);
        }
        Assert.InRange(waterC.WrittenTime, waterA.WrittenTime - 1f, waterA.WrittenTime + 1f);
        float beforeChange = waterC.WrittenTime;
        Assert.True(beforeChange > 250f);

        waterB.Installed = true;
        Advance(b);
        Assert.True(waterB.WrittenTime < 1f);

        battle.MigrateHost(a, "B");
        for (int tick = 0; tick < 100; tick++)
        {
            Advance(b);
            Advance(c);
        }

        Assert.True(waterB.WrittenTime > beforeChange);
        Assert.InRange(waterC.WrittenTime, waterB.WrittenTime - 1f, waterB.WrittenTime + 1f);
        AssertNeverBackwards(waterA);
        AssertNeverBackwards(waterB);
        AssertNeverBackwards(waterC);
    }

    // Host samples keep coming while only the follower's clock stalls mid-battle; on resume it catches up without a jump.
    [Fact]
    public void FollowerClockStall_CatchesUpWithoutAMidBattleWaterJump()
    {
        using var battle = StartNavalBattle(new[] { ("A", BattleSideEnum.Attacker), ("B", BattleSideEnum.Attacker) });
        var a = battle["A"];
        var b = battle["B"];
        var waterA = WaterOf(a);
        var waterB = WaterOf(b);
        waterA.Installed = true;
        waterA.Vanilla = 5000f;
        waterB.Installed = true;
        waterB.Vanilla = 300f;

        // 20 s with A hosting, so B has adopted A's time and settled on it.
        for (int tick = 0; tick < 400; tick++)
        {
            Advance(a);
            Advance(b);
        }
        Assert.InRange(waterB.WrittenTime, waterA.WrittenTime - 1f, waterA.WrittenTime + 1f);
        int settledWrites = waterB.Written.Count;

        // B's mission clock stands for 5 s, as vanilla ticks a paused mission with a zero step, while A's samples keep coming.
        for (int tick = 0; tick < 100; tick++)
        {
            Advance(a);
            b.Tick(0f);
        }

        for (int tick = 0; tick < 600; tick++)
        {
            Advance(a);
            Advance(b);
        }

        // Waves never run faster than double speed mid-battle, so one tick moves the water at most two ticks' worth.
        float maxStep = 2f * TickSeconds * FakeWaterClock.UnitsPerSecond;
        for (int i = settledWrites; i < waterB.Written.Count; i++)
        {
            float step = waterB.Written[i] - waterB.Written[i - 1];
            Assert.True(step <= maxStep + 0.01f, $"water jumped {step} at write {i}: {waterB.Written[i - 1]} to {waterB.Written[i]}");
        }
        Assert.InRange(waterB.WrittenTime, waterA.WrittenTime - 1f, waterA.WrittenTime + 1f);
        AssertNeverBackwards(waterB);
    }

    // The client's in-memory water clock, which the harness registers per client.
    private static FakeWaterClock WaterOf(NavalPeer peer) => peer.Instance.Resolve<FakeWaterClock>();

    // One mission tick on one client: its vanilla clock moves, then its naval services tick.
    private static void Advance(NavalPeer peer)
    {
        WaterOf(peer).Elapse(TickSeconds);
        peer.Tick();
    }

    // Each written water time is at least the one before it.
    private static void AssertNeverBackwards(FakeWaterClock water)
    {
        for (int i = 1; i < water.Written.Count; i++)
            Assert.True(water.Written[i] >= water.Written[i - 1], $"water ran backwards at write {i}: {water.Written[i - 1]} to {water.Written[i]}");
    }
}
