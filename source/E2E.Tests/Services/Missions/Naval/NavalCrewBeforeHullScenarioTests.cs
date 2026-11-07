using Missions.Battles;
using Missions.Messages;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions.Naval;

/// <summary>
/// A hull's crew record can reach a peer before the hull spawns there. The peer's puppet spawner buffers the crew until
/// the hull registers, and drops it once the replicator gives up spawning the hull (M2 K7).
/// </summary>
public class NavalCrewBeforeHullScenarioTests : NavalBattleTestEnvironment
{
    public NavalCrewBeforeHullScenarioTests(ITestOutputHelper output) : base(output, numClients: 2) { }

    [Fact]
    public void CrewRecord_BeforeItsHull_IsHeldAndSpawnsOnceTheHullRegisters()
    {
        using var battle = StartNavalBattle(new[] { ("A", BattleSideEnum.Attacker), ("B", BattleSideEnum.Attacker) });
        var a = battle["A"];
        var b = battle["B"];
        b.Mission.DeploymentInProgress = true;
        b.Mission.DeploymentController.TeamSetupOver = false;
        var pirate = battle.FieldAiHull(a, BattleSideEnum.Defender, new Vec3(0f, 300f, 0f));
        var crewId = Guid.NewGuid();
        SendCrew(battle, a, pirate, crewId);

        b.TickController();
        Assert.False(b.Has(pirate));
        Assert.False(b.Agents.TryGetAgentInfo(crewId, out _));

        b.Mission.DeploymentController.TeamSetupOver = true;
        b.Tick();
        b.TickController();

        Assert.True(b.Has(pirate));
        Assert.True(b.Agents.TryGetAgentInfo(crewId, out var crew));
        Assert.Equal("A", crew.CurrentAuthority);
    }

    [Fact]
    public void HullSpawn_ThatKeepsFailing_RetriesThenDropsItsBufferedCrew()
    {
        using var battle = StartNavalBattle(new[] { ("A", BattleSideEnum.Attacker), ("B", BattleSideEnum.Attacker) });
        var a = battle["A"];
        var b = battle["B"];
        b.Engine.FailingForeignSpawns = int.MaxValue;
        battle.ExpectLoggedFailure("Could not spawn hull");
        var pirate = battle.FieldAiHull(a, BattleSideEnum.Defender, new Vec3(0f, 300f, 0f));
        var crewId = Guid.NewGuid();
        SendCrew(battle, a, pirate, crewId);

        b.TickController();
        Assert.False(b.Agents.TryGetAgentInfo(crewId, out _));
        for (int retry = 1; retry < BattleShipReplicator.ForeignHullSpawnAttempts; retry++)
        {
            Assert.False(b.Registry.StateOf(pirate) == ShipLifecycle.SpawnFailed);
            b.Tick(BattleShipReplicator.ForeignHullRetrySeconds + 0.1f);
        }

        Assert.True(b.Registry.StateOf(pirate) == ShipLifecycle.SpawnFailed);
        Assert.Equal(BattleShipReplicator.ForeignHullSpawnAttempts, b.Engine.ForeignSpawnAttempts);

        b.TickController();
        b.Tick(BattleShipReplicator.ForeignHullRetrySeconds + 0.1f);
        Assert.Equal(BattleShipReplicator.ForeignHullSpawnAttempts, b.Engine.ForeignSpawnAttempts);
        Assert.False(b.Has(pirate));

        // The host's deployment commit announces the hull again and it spawns now, but the dropped crew stays dropped.
        b.Engine.FailingForeignSpawns = 0;
        a.Commit();
        a.Tick();
        b.TickArrivals();
        b.TickController();

        Assert.True(b.Has(pirate));
        Assert.False(b.Agents.TryGetAgentInfo(crewId, out _));
    }

    // The battle host's crew record for one hull, encoded and sent over the mesh like its owned-agent replication.
    private void SendCrew(NavalBattle battle, NavalPeer host, Guid shipId, Guid crewId)
    {
        var characterId = CreateRegisteredObject<CharacterObject>();
        host.Call(() =>
        {
            var record = new BattleAgentSpawnData(crewId, characterId, default, BattleSideEnum.Defender, 100f, host.Id,
                battle.PirateMapEventPartyId, 1, new Equipment(), new BodyProperties(), missionEquipmentData: null,
                shipId: shipId);
            foreach (var batch in host.Instance.Resolve<IBattleAgentSpawnBatchCodec>().Encode(new[] { record }, SpawnBatchPurpose.Initial))
                host.Network.SendAll(batch);
        });
    }
}
