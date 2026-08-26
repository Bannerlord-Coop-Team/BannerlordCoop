using Common;
using Common.Tests.Utils;
using Common.Util;
using Coop.Tests;
using GameInterface.Services.MapEvents;
using GameInterface.Services.MapEvents.Handlers;
using GameInterface.Services.MapEvents.Messages;
using GameInterface.Services.MapEvents.Messages.Start;
using GameInterface.Services.ObjectManager;
using Moq;
using System;
using System.Xml;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using Xunit;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace Coop.Tests.Server.Services.MapEvents;

/// <summary>
/// BR-102 (Host Epoch and Stale Host Rejection) for the siege engine write-back: the mission host's
/// <see cref="NetworkSiegeEngineStatesReport"/> drives a non-idempotent campaign write-back
/// (<c>SiegeEvent.SetSiegeEngineStatesAfterSiegeMission</c>), so the server must refuse a report
/// stamped by a stale hosting generation before it reaches that write-back. Identity alone cannot
/// close the A → B → A re-promotion hole (a report in flight from the sender's EARLIER hosting
/// stint arrives while that same player is host again), so the epoch is the gate — the sibling of
/// the tested <c>NetworkChangeBattleState</c> gate (E2E <c>HostEpochStaleConclusionTests</c>).
/// <para>
/// The full write-back needs a live siege mission/scene neither harness provides, so this drives
/// the real handler with a mocked object manager: "refused" = the handler returned before the
/// write-back's map-event lookup; "applied" = the lookup ran. The handler's stamping counterpart
/// (<c>SiegeEngineStateReporter.ReportConcludedIfHost</c>) needs <c>Mission.Current</c> plus
/// <c>MissionSiegeEnginesLogic</c> and stays covered by code review only.
/// </para>
/// </summary>
[Collection(ModInformationRoleCollection.Name)]
public class SiegeEngineStateCommitHandlerTests : IDisposable
{
    private const string MapEventId = "map-event-1";

    private readonly TestMessageBroker broker = new();
    private readonly Mock<IObjectManager> objectManager = new();
    private readonly Mock<IBattleHostRegistry> hostRegistry = new();
    private readonly SiegeEngineStateCommitHandler handler;

    public SiegeEngineStateCommitHandlerTests()
    {
        handler = new SiegeEngineStateCommitHandler(broker, objectManager.Object, hostRegistry.Object);
    }

    [Fact]
    [Trait("Requirement", "BR-102")]
    public void StaleEpochReport_IsRefusedBeforeTheCampaignWriteBack_AndTheCurrentEpochReportApplies()
    {
        // The battle's assignment history is host-A -> host-B -> host-A again (two migrations), so the
        // CURRENT assignment carries epoch 3 while the sender's first stint was epoch 1. The sender IS
        // the current host, so only the epoch can refuse its stale report.
        var assignment = new BattleHostAssignment("host-A", Array.Empty<string>(), epoch: 3);
        hostRegistry.Setup(r => r.TryGet(MapEventId, out assignment)).Returns(true);

        // A report from the sender's FIRST hosting stint (epoch 1) arrives after the migrations.
        PublishAsServer(Report(hostEpoch: 1));
        DrainGameThread();

        // Refused BEFORE the write-back: the handler never even resolved the map event.
        objectManager.Verify(
            manager => manager.TryGetObjectWithLogging<MapEvent>(It.IsAny<string>(), out It.Ref<MapEvent>.IsAny),
            Times.Never);

        // The SAME report stamped with the CURRENT epoch is honored — the write-back path resolves the
        // map event — proving the refusal above is the stale-epoch gate, not a blanket rejection.
        PublishAsServer(Report(hostEpoch: 3));
        DrainGameThread();

        objectManager.Verify(
            manager => manager.TryGetObjectWithLogging<MapEvent>(MapEventId, out It.Ref<MapEvent>.IsAny),
            Times.Once);
    }

    [Fact]
    [Trait("Requirement", "BR-102")]
    public void ReportForABattleWithoutAnAssignment_IsApplied()
    {
        // No host assignment recorded (e.g. the battle already tore down its assignment): there is no
        // epoch to judge against, so the report proceeds to the write-back lookup.
        PublishAsServer(Report(hostEpoch: 0));
        DrainGameThread();

        objectManager.Verify(
            manager => manager.TryGetObjectWithLogging<MapEvent>(MapEventId, out It.Ref<MapEvent>.IsAny),
            Times.Once);
    }

    [Fact]
    public void AmbushReport_UpdatesSurvivingMeleeEngineHitPoints()
    {
        var engineType = new SiegeEngineType();
        var engine = new SiegeEngineConstructionProgress(engineType, progress: 1f, maxHitPoints: 100f);
        var container = new SiegeEnginesContainer(BattleSideEnum.Attacker, siegePreparations: null);
        container.DeploySiegeEngineAtIndex(engine, index: 0);
        var side = new Mock<ISiegeEventSide>();
        side.SetupGet(value => value.SiegeEngines).Returns(container);
        var missionWeapon = MissionSiegeWeapon.CreateCampaignWeapon(engineType, 0, 45f, 100f);

        SiegeEngineStateCommitHandler.SetAmbushEngineStatesForSide(
            ObjectHelper.SkipConstructor<SiegeEvent>(),
            side.Object,
            new[] { missionWeapon });

        Assert.Equal(45f, engine.Hitpoints);
        Assert.Contains(engine, container.DeployedSiegeEngines);
    }

    [Fact]
    public void AmbushReport_RemovesDestroyedDuplicateByMissionIndex()
    {
        var engineType = new SiegeEngineType();
        var destroyedEngine = new SiegeEngineConstructionProgress(engineType, progress: 1f, maxHitPoints: 100f);
        var survivingEngine = new SiegeEngineConstructionProgress(engineType, progress: 1f, maxHitPoints: 100f);
        var container = new SiegeEnginesContainer(BattleSideEnum.Attacker, siegePreparations: null);
        container.DeploySiegeEngineAtIndex(destroyedEngine, index: 0);
        container.DeploySiegeEngineAtIndex(survivingEngine, index: 1);
        var side = new Mock<ISiegeEventSide>();
        side.SetupGet(value => value.SiegeEngines).Returns(container);
        var missionWeapons = new[]
        {
            MissionSiegeWeapon.CreateCampaignWeapon(engineType, 0, 0f, 100f),
            MissionSiegeWeapon.CreateCampaignWeapon(engineType, 1, 45f, 100f),
        };

        SiegeEngineStateCommitHandler.SetAmbushEngineStatesForSide(
            ObjectHelper.SkipConstructor<SiegeEvent>(),
            side.Object,
            missionWeapons);

        Assert.Null(container.DeployedMeleeSiegeEngines[0]);
        Assert.Same(survivingEngine, container.DeployedMeleeSiegeEngines[1]);
        Assert.DoesNotContain(destroyedEngine, container.DeployedSiegeEngines);
        Assert.Contains(survivingEngine, container.DeployedSiegeEngines);
        Assert.Equal(45f, survivingEngine.Hitpoints);
    }

    [Fact]
    public void AmbushReport_UsesCompactMissionIndicesForSparseMixedDeploymentSlots()
    {
        var rangedType = CreateSiegeEngineType("test_ranged", isRanged: true);
        var meleeType = CreateSiegeEngineType("test_melee", isRanged: false);
        var rangedEngine = new SiegeEngineConstructionProgress(rangedType, progress: 1f, maxHitPoints: 100f);
        var meleeEngine = new SiegeEngineConstructionProgress(meleeType, progress: 1f, maxHitPoints: 100f);
        var container = new SiegeEnginesContainer(BattleSideEnum.Attacker, siegePreparations: null);
        container.DeploySiegeEngineAtIndex(rangedEngine, index: 3);
        container.DeploySiegeEngineAtIndex(meleeEngine, index: 0);
        var side = new Mock<ISiegeEventSide>();
        side.SetupGet(value => value.SiegeEngines).Returns(container);
        var missionWeapons = new[]
        {
            MissionSiegeWeapon.CreateCampaignWeapon(rangedType, 0, 0f, 100f),
            MissionSiegeWeapon.CreateCampaignWeapon(meleeType, 1, 45f, 100f),
        };

        SiegeEngineStateCommitHandler.SetAmbushEngineStatesForSide(
            ObjectHelper.SkipConstructor<SiegeEvent>(),
            side.Object,
            missionWeapons);

        Assert.Null(container.DeployedRangedSiegeEngines[3]);
        Assert.Same(meleeEngine, container.DeployedMeleeSiegeEngines[0]);
        Assert.Equal(45f, meleeEngine.Hitpoints);
    }

    private static SiegeEngineType CreateSiegeEngineType(string id, bool isRanged)
    {
        var document = new XmlDocument();
        document.LoadXml(
            $"<SiegeEngine id=\"{id}\" name=\"{id}\" description=\"{id}\" max_hit_points=\"100\" " +
            "difficulty=\"0\" tool_cost=\"0\" hit_chance=\"0\" is_anti_personnel=\"false\" " +
            $"is_constructible=\"true\" is_ranged=\"{isRanged}\" damage=\"0\" man_day_cost=\"0\" " +
            "campaign_rate_of_fire_per_day=\"0\" movement_speed=\"0\" projectile_speed=\"0\" />");
        var engineType = new SiegeEngineType();
        engineType.Deserialize(null, document.DocumentElement);
        return engineType;
    }

    private static NetworkSiegeEngineStatesReport Report(int hostEpoch)
        => new(MapEventId, Array.Empty<SiegeEngineState>(), Array.Empty<SiegeEngineState>(), hostEpoch);

    /// <summary>
    /// The handler drops every report on a client (<c>ModInformation.IsClient</c>), so flip the global
    /// role flag for exactly the synchronous publish (the role check runs inline; the gated body runs
    /// later on the test game-loop pump and does not consult it), keeping the window for parallel
    /// tests that read the flag as small as possible.
    /// </summary>
    private void PublishAsServer(NetworkSiegeEngineStatesReport report)
    {
        ModInformation.IsServer = true;
        try
        {
            broker.Publish(this, report);
        }
        finally
        {
            ModInformation.IsServer = false;
        }
    }

    /// <summary>
    /// The handler queues its gated body via <c>GameThread.RunSafe</c>; a blocking no-op queued after
    /// it (FIFO) proves the body has run on the test game-loop pump before the assertions read.
    /// </summary>
    private static void DrainGameThread() => GameThread.Run(() => { }, blocking: true);

    public void Dispose() => handler.Dispose();
}
