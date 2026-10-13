using Missions.Messages;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions.Naval;

/// <summary>
/// M4d deployment hold: a client still deploying holds other owners' hull records with their latest sample, condition
/// and final ropes, and spawns each copy once its deployment ends.
/// </summary>
public class NavalDeploymentHoldScenarioTests : NavalBattleTestEnvironment
{
    public NavalDeploymentHoldScenarioTests(ITestOutputHelper output) : base(output, numClients: 2) { }

    [Fact]
    public void ForeignHulls_HeldWhileDeploying_SpawnOnceWithTheLatestHeldState()
    {
        using var battle = StartNavalBattle(new[] { ("A", BattleSideEnum.Attacker), ("B", BattleSideEnum.Attacker) });
        var a = battle["A"];
        var b = battle["B"];
        b.Engine.IsDeploymentMode = true;
        var hullA = battle.FieldPlayerHull(a, new Vec3(0f, 0f, 0f));
        var pirate = battle.FieldAiHull(a, BattleSideEnum.Defender, new Vec3(0f, 300f, 0f));
        a.Hull(hullA).Ropes = new[] { Rope(generation: 1) };
        a.Commit();

        for (int step = 1; step <= 3; step++)
        {
            a.Hull(hullA).Frame = new MatrixFrame(Mat3.Identity, new Vec3(step, 0f, 0f));
            a.Hull(pirate).HitPoints = 100f - (20f * step);
            battle.TickAll();
        }
        a.Hull(hullA).Ropes = new[] { Rope(generation: 2) };
        a.Call(a.Ships.SendFinalRopes);

        Assert.Equal(0, b.Engine.ForeignSpawnAttempts);
        Assert.False(b.Has(hullA));
        Assert.False(b.Has(pirate));

        b.Engine.IsDeploymentMode = false;
        b.Tick();
        b.Tick();

        Assert.Equal(2, b.Engine.ForeignSpawnAttempts);
        Assert.Equal(2, b.Engine.Copies.Count);
        Assert.Equal(3f, b.Hull(hullA).Frame.origin.x, 3);
        Assert.Equal(a.SentSamples(hullA).Last().Sequence, (long)b.Stream(hullA)["accepted"]!);
        Assert.Equal(40f, b.Hull(pirate).HitPoints, 3);
        Assert.Equal(1, b.Hull(pirate).AppliedConditions);
        Assert.Equal(1, b.Hull(hullA).FinalRopesApplied);
        Assert.Equal(2, b.Hull(hullA).Ropes.Single().Generation);
    }

    [Fact]
    public void HostMigration_WhileTheAiRecordIsHeld_TheNewHostTakesTheHullOverFromTheDepartedHostsState()
    {
        using var battle = StartNavalBattle(new[] { ("A", BattleSideEnum.Attacker), ("B", BattleSideEnum.Attacker) });
        var a = battle["A"];
        var b = battle["B"];
        b.Engine.IsDeploymentMode = true;
        var pirate = battle.FieldAiHull(a, BattleSideEnum.Defender, new Vec3(0f, 300f, 0f));
        a.Commit();
        a.Hull(pirate).Frame = new MatrixFrame(Mat3.Identity, new Vec3(5f, 300f, 0f));
        a.Hull(pirate).HitPoints = 70f;
        battle.TickAll(2);

        battle.MigrateHost(a, "B");
        b.Engine.IsDeploymentMode = false;
        b.Commit();
        b.Tick();

        var takenOver = b.Ship(pirate);
        Assert.Equal("B", takenOver.CurrentAuthority);
        Assert.Equal(5f, b.Engine.Of(takenOver.Hull).Frame.origin.x, 3);
        Assert.Equal(70f, b.Engine.Of(takenOver.Hull).HitPoints, 3);

        b.Ticks(3);
        var simulated = b.Hull(pirate);
        Assert.False(simulated.IsCopy);
        Assert.Equal(5f, simulated.Frame.origin.x, 3);
        Assert.Equal(70f, simulated.HitPoints, 3);
        Assert.All(b.SentSamples(pirate), sample => Assert.Equal(battle.HostEpoch, sample.HostEpoch));
        Assert.NotEmpty(b.SentSamples(pirate));
    }

    private static BattleRopeState Rope(long generation) => new()
    {
        SourceKey = "rope_station_0",
        Generation = generation,
        State = BattleRopeState.RopeThrown,
        Length = 5f,
        HookFrame = NetworkBattleShipSample.FromFrame(MatrixFrame.Identity),
    };
}
