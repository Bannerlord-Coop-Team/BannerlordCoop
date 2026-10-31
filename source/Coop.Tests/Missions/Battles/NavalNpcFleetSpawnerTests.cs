#if COOP_NAVAL
using Common;
using Common.Messaging;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using HarmonyLib;
using Missions;
using Missions.Battles;
using Missions.Messages;
using Missions.Naval;
using Moq;
using ProtoBuf;
using System.IO;
using System.Linq;
using TaleWorlds.Core;
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class NavalNpcFleetSpawnerTests
{
    [Theory]
    [InlineData(false, true, true, 2, NavalNpcFleetSpawner.WaitingForHost)]
    [InlineData(false, false, false, 0, NavalNpcFleetSpawner.WaitingForHost)]
    [InlineData(true, false, true, 2, NavalNpcFleetSpawner.WaitingForDeployment)]
    [InlineData(true, true, false, 2, NavalNpcFleetSpawner.WaitingForReserve)]
    [InlineData(true, true, true, 0, NavalNpcFleetSpawner.WaitingForReserve)]
    [InlineData(true, true, true, 1, NavalNpcFleetSpawner.Ready)]
    public void Decide_FieldsTheAiFleetOnlyOnTheHostAfterDeploymentWithTheNpcReserve(
        bool isLocalHost, bool deploymentFinished, bool reservePopulated, int npcParties, string expected)
    {
        Assert.Equal(expected, NavalNpcFleetSpawner.Decide(isLocalHost, deploymentFinished, reservePopulated, npcParties));
    }

    [Theory]
    [InlineData(4, 3, 40, 8, 3)]
    [InlineData(2, 3, 40, 8, 2)]
    [InlineData(4, 3, 2, 8, 2)]
    [InlineData(4, 3, 40, 1, 1)]
    [InlineData(12, 12, 400, 11, 8)]
    [InlineData(4, 3, 0, 8, 0)]
    [InlineData(0, 3, 40, 8, 0)]
    public void ShipCount_FollowsVanillasLimitShipTroopAndFormationCaps(int limit, int ships, int troops, int formations, int expected)
    {
        Assert.Equal(expected, NavalNpcFleetSpawner.ShipCount(limit, ships, troops, formations));
    }

    [Theory]
    [InlineData(0, null, NavalNpcFleetSpawner.Failed)]
    [InlineData(0, "no hull spawned", NavalNpcFleetSpawner.Failed)]
    [InlineData(2, "NullReferenceException: crewing threw", NavalNpcFleetSpawner.Failed)]
    [InlineData(3, null, NavalNpcFleetSpawner.Fielded)]
    public void Outcome_FailsAnAttemptThatThrewEvenAfterItsHullsSpawned(int hullsSpawned, string failure, string expected)
    {
        Assert.Equal(expected, NavalNpcFleetSpawner.Outcome(hullsSpawned, failure));
    }

    [Fact]
    public void FailedFleetThenMigration_SuccessorNeverFieldsAndReleasesTheHold()
    {
        // A PvP client on the defender side: its own enemy side is the attacker, the battle host's failed fleet the defender's.
        var harness = new Harness(BattleSideEnum.Attacker);
        harness.Broker.Publish(this, new NetworkNpcFleetAttempted(BattleSideEnum.Defender, failed: true));
        GameThread.Run(() => { }, blocking: true);

        harness.IsLocalHost = true;
        harness.Spawner.Tick();

        Assert.True(harness.Spawner.HasAttempt);
        Assert.Equal(BattleSideEnum.Defender, harness.Spawner.FailedSide);
        Assert.True(CoopNavalBattleEndLogic.ShouldReleaseHold(true, attackerFielded: true, defenderFielded: false,
            harness.Spawner.FailedSide));
        harness.Network.Verify(network => network.SendAll(It.IsAny<IMessage>()), Times.Never);
    }

    [Fact]
    public void AcceptAttempt_OfAFieldedFleet_SettlesWithoutAFailedSide_AndKeepsTheFirstAttempt()
    {
        var harness = new Harness(BattleSideEnum.Defender);

        harness.Spawner.AcceptAttempt(new NetworkNpcFleetAttempted(BattleSideEnum.Defender, failed: false));
        harness.Spawner.AcceptAttempt(new NetworkNpcFleetAttempted(BattleSideEnum.Defender, failed: true));

        Assert.True(harness.Spawner.HasAttempt);
        Assert.Equal(BattleSideEnum.None, harness.Spawner.FailedSide);
    }

    [Theory]
    [InlineData(true, true, "joiner", true)]
    [InlineData(true, false, "joiner", true)]
    [InlineData(false, true, "joiner", false)]
    [InlineData(true, true, Own, false)]
    public void SendAttemptTo_EveryClientHoldingTheAttemptPassesItToAnotherEntrant(bool attempted, bool isLocalHost, string entrant,
        bool sends)
    {
        var harness = new Harness(BattleSideEnum.Defender) { IsLocalHost = isLocalHost };
        if (attempted) harness.Spawner.AcceptAttempt(new NetworkNpcFleetAttempted(BattleSideEnum.Defender, failed: true));

        harness.Spawner.SendAttemptTo(entrant);

        harness.Network.Verify(network => network.Send(entrant, It.Is<NetworkNpcFleetAttempted>(sent =>
            sent.Side == BattleSideEnum.Defender && sent.Failed)), sends ? Times.Once() : Times.Never());
    }

    [Fact]
    public void Attempt_RoundTripsOverTheWire()
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, new NetworkNpcFleetAttempted(BattleSideEnum.Attacker, failed: true));
        stream.Position = 0;

        var copy = Serializer.Deserialize<NetworkNpcFleetAttempted>(stream);

        Assert.Equal(BattleSideEnum.Attacker, copy.Side);
        Assert.True(copy.Failed);
    }

    [Fact]
    public void LatePlanNotificationPatch_BindsAndOnlySilencesTheLatePlan()
    {
        Assert.False(NavalLateDeploymentPlan.IsMaking);
        Assert.True(LateDeploymentPlanNotificationPatch.Prefix());

        var harmony = new Harmony("coop.tests.naval.late_plan");
        try
        {
            Assert.NotEmpty(harmony.CreateClassProcessor(typeof(LateDeploymentPlanNotificationPatch)).Patch());
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

#if DEBUG
    // A forced attempt must take the real catch, broadcast, hold-release and successor/entrant paths.
    [Fact]
    public void ForcedFailure_BroadcastsOnce_ReleasesHold_AndSurvivesMigrationAndLateJoin()
    {
        var harness = new Harness(BattleSideEnum.Defender) { IsLocalHost = true };
        Assert.Null(harness.Spawner.FailNextSpawn());
        bool spawned = false;
        harness.Spawner.AttemptFleet(() => spawned = true);
        harness.Spawner.AttemptFleet(() => spawned = true);

        Assert.False(spawned);
        Assert.Equal(BattleSideEnum.Defender, harness.Spawner.FailedSide);
        Assert.True(CoopNavalBattleEndLogic.ShouldReleaseHold(true, true, false, harness.Spawner.FailedSide));
        harness.Network.Verify(network => network.SendAll(It.Is<NetworkNpcFleetAttempted>(sent => sent.Failed)), Times.Once);
        Assert.NotNull(harness.Spawner.FailNextSpawn());

        var successor = new Harness(BattleSideEnum.Defender) { IsLocalHost = true };
        successor.Spawner.AcceptAttempt(new NetworkNpcFleetAttempted(BattleSideEnum.Defender, failed: true));
        successor.Spawner.AttemptFleet(() => spawned = true);
        successor.Spawner.SendAttemptTo("late_joiner");

        Assert.False(spawned);
        successor.Network.Verify(network => network.SendAll(It.IsAny<IMessage>()), Times.Never);
        successor.Network.Verify(network => network.Send("late_joiner", It.Is<NetworkNpcFleetAttempted>(sent =>
            sent.Side == BattleSideEnum.Defender && sent.Failed)), Times.Once);
        var inspect = Newtonsoft.Json.Linq.JObject.FromObject(harness.Spawner.Inspect());
        Assert.Equal("failed", (string)inspect["state"]);
        Assert.Contains("DEBUG fleet_fail", (string)inspect["failure"]);
    }

    // Scenario intent is read at the attempt, rather than captured before the scenario message arrives.
    [Fact]
    public void ScenarioFailure_IsReadAtTheAttempt_AndUsesTheNormalFailurePath()
    {
        bool failScenario = false;
        var harness = new Harness(BattleSideEnum.Defender, () => failScenario) { IsLocalHost = true };
        failScenario = true;
        bool spawned = false;

        harness.Spawner.AttemptFleet(() => spawned = true);

        Assert.False(spawned);
        Assert.Equal(BattleSideEnum.Defender, harness.Spawner.FailedSide);
        harness.Network.Verify(network => network.SendAll(It.Is<NetworkNpcFleetAttempted>(sent => sent.Failed)), Times.Once);
    }

    // A non-host cannot arm the attempt, and a fresh mission does not inherit another spawner's debug flag.
    [Fact]
    public void ForcedFailure_RejectsNonHost_AndIsScopedToOneMission()
    {
        var nonHost = new Harness(BattleSideEnum.Defender);
        Assert.NotNull(nonHost.Spawner.FailNextSpawn());
        bool spawned = false;
        nonHost.Spawner.AttemptFleet(() => spawned = true);
        Assert.True(spawned);

        var armed = new Harness(BattleSideEnum.Defender) { IsLocalHost = true };
        Assert.Null(armed.Spawner.FailNextSpawn());
        var fresh = new Harness(BattleSideEnum.Defender) { IsLocalHost = true };
        spawned = false;
        fresh.Spawner.AttemptFleet(() => spawned = true);
        Assert.True(spawned);
    }
#endif

    private const string Own = "own";

    // A spawner over a mocked session and network, with a real broker for the attempt subscription.
    private sealed class Harness
    {
        public Mock<IBattleNetwork> Network { get; } = new Mock<IBattleNetwork>();
        public MessageBroker Broker { get; } = new MessageBroker();
        public bool IsLocalHost { get; set; }
        public NavalNpcFleetSpawner Spawner { get; }

        public Harness(BattleSideEnum enemySide, System.Func<bool> scenarioFailsSpawn = null)
        {
            var session = new Mock<IBattleSession>();
            session.SetupGet(s => s.IsLocalHost).Returns(() => IsLocalHost);
            session.SetupGet(s => s.InstanceId).Returns("MapEvent_1");
            session.Setup(s => s.IsOwn(It.IsAny<string>())).Returns((string id) => id == Own);
            Spawner = new NavalNpcFleetSpawner(Network.Object, Broker, session.Object, Mock.Of<IBattleShipReplicator>(),
                Mock.Of<ICoopShipSnapshotBuilder>(), Mock.Of<IObjectManager>(), Mock.Of<IPlayerManager>(), enemySide
#if DEBUG
                , scenarioFailsSpawn
#endif
                );
        }
    }
}
#endif
