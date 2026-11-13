#if COOP_NAVAL
using Common;
using Common.Messaging;
using Common.Network;
#if DEBUG
using GameInterface.Services.MapEvents.NavalScenarios;
#endif
using HarmonyLib;
using Missions;
using Missions.Battles;
using Missions.Messages;
using Missions.Naval;
using Moq;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
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
    [InlineData(4, 3, 8, 3)]
    [InlineData(2, 3, 8, 2)]
    [InlineData(4, 3, 1, 1)]
    [InlineData(12, 12, 11, 8)]
    [InlineData(0, 3, 8, 0)]
    public void ShipCount_FollowsVanillasLimitShipAndFormationCaps(int limit, int ships, int formations, int expected)
    {
        Assert.Equal(expected, NavalNpcFleetFielder.ShipCount(limit, ships, formations));
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

    // AI on both sides (an allied lord and the enemy): an earlier host's attempt latches only its own side's spawner, so the
    // promoted host fields the side nobody attempted and never the latched one (the only one of these checks that also runs in
    // Release, outside the #if DEBUG tests).
    [Fact]
    public void AnAttempt_LatchesOnlyItsOwnSidesSpawner_AndAPromotedHostFieldsOnlyTheOtherSide()
    {
        var broker = new MessageBroker();
        var attacker = new Harness(BattleSideEnum.Attacker, broker: broker);
        var defender = new Harness(BattleSideEnum.Defender, broker: broker);
        broker.Publish(this, Attempted(BattleSideEnum.Defender, failed: true));
        GameThread.Run(() => { }, blocking: true);

        Assert.False(attacker.Spawner.HasAttempt);
        Assert.True(defender.Spawner.AttemptFailed);

        attacker.IsLocalHost = true;
        defender.IsLocalHost = true;
        var spawned = new List<BattleSideEnum>();
        attacker.Attempt(_ => { spawned.Add(BattleSideEnum.Attacker); return null; });
        defender.Attempt(_ => { spawned.Add(BattleSideEnum.Defender); return null; });

        Assert.Equal(new[] { BattleSideEnum.Attacker }, spawned);
    }

    [Fact]
    public void AcceptAttempt_OfAFieldedFleet_SettlesNotFailed_AndKeepsTheFirstAttempt()
    {
        var harness = new Harness(BattleSideEnum.Defender);

        harness.Spawner.AcceptAttempt(Attempted(BattleSideEnum.Defender, failed: false));
        harness.Spawner.AcceptAttempt(Attempted(BattleSideEnum.Defender, failed: true));

        Assert.True(harness.Spawner.HasAttempt);
        Assert.False(harness.Spawner.AttemptFailed);
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
        if (attempted) harness.Spawner.AcceptAttempt(Attempted(BattleSideEnum.Defender, failed: true));

        harness.Spawner.SendAttemptTo(entrant);

        harness.Network.Verify(network => network.Send(entrant, It.Is<NetworkNpcFleetAttempted>(sent =>
            sent.Side == BattleSideEnum.Defender && sent.Failed)), sends ? Times.Once() : Times.Never());
    }

    [Fact]
    public void Attempt_RoundTripsOverTheWire()
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, new NetworkNpcFleetAttempted("MapEvent_1", BattleSideEnum.Attacker, failed: true, aiHulls: 2,
            playerHulls: 3, countedPartyIds: new[] { "MobileParty_1", "MobileParty_2" }));
        stream.Position = 0;

        var copy = Serializer.Deserialize<NetworkNpcFleetAttempted>(stream);

        Assert.Equal(("MapEvent_1", BattleSideEnum.Attacker, true, 2, 3), (copy.InstanceId, copy.Side, copy.Failed, copy.AiHulls,
            copy.PlayerHulls));
        Assert.Equal(new[] { "MobileParty_1", "MobileParty_2" }, copy.CountedPartyIds);
    }

    // This battle's attempt of the side, counting no player.
    private static NetworkNpcFleetAttempted Attempted(BattleSideEnum side, bool failed) =>
        new NetworkNpcFleetAttempted("MapEvent_1", side, failed, aiHulls: 0, playerHulls: 0, countedPartyIds: null);

    [Fact]
    public void LatePlanNotificationPatch_BindsAndOnlySilencesTheLatePlan()
    {
        using var naval = new NavalClientContainer();
        Assert.False(naval.Resolve<INavalLateDeploymentPlanTracker>().IsMaking);
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
    // A forced attempt must take the real catch, broadcast and successor/entrant paths.
    [Fact]
    public void ForcedFailure_BroadcastsOnce_AndSurvivesMigrationAndLateJoin()
    {
        var harness = new Harness(BattleSideEnum.Defender) { IsLocalHost = true };
        Assert.Null(harness.Spawner.FailNextSpawn());
        bool spawned = false;
        harness.Attempt(_ => { spawned = true; return null; });
        harness.Attempt(_ => { spawned = true; return null; });

        Assert.False(spawned);
        Assert.True(harness.Spawner.AttemptFailed);
        harness.Network.Verify(network => network.SendAll(It.Is<NetworkNpcFleetAttempted>(sent => sent.Failed)), Times.Once);
        Assert.NotNull(harness.Spawner.FailNextSpawn());

        var successor = new Harness(BattleSideEnum.Defender) { IsLocalHost = true };
        successor.Spawner.AcceptAttempt(Attempted(BattleSideEnum.Defender, failed: true));
        successor.Attempt(_ => { spawned = true; return null; });
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

        harness.Attempt(_ => { spawned = true; return null; });

        Assert.False(spawned);
        Assert.True(harness.Spawner.AttemptFailed);
        harness.Network.Verify(network => network.SendAll(It.Is<NetworkNpcFleetAttempted>(sent => sent.Failed)), Times.Once);
    }

    // A non-host cannot arm the attempt, and a fresh mission does not inherit another spawner's debug flag.
    [Fact]
    public void ForcedFailure_RejectsNonHost_AndIsScopedToOneMission()
    {
        var nonHost = new Harness(BattleSideEnum.Defender);
        Assert.NotNull(nonHost.Spawner.FailNextSpawn());
        bool spawned = false;
        nonHost.Attempt(_ => { spawned = true; return null; });
        Assert.True(spawned);

        var armed = new Harness(BattleSideEnum.Defender) { IsLocalHost = true };
        Assert.Null(armed.Spawner.FailNextSpawn());
        var fresh = new Harness(BattleSideEnum.Defender) { IsLocalHost = true };
        spawned = false;
        fresh.Attempt(_ => { spawned = true; return null; });
        Assert.True(spawned);
    }
#endif

    // A failure after the first hull registered takes that hull back before peers hear the attempt failed.
    [Fact]
    public void FailureAfterTheFirstHull_TakesBackThatHull_BeforeBroadcastingFailed()
    {
        var harness = new Harness(BattleSideEnum.Defender) { IsLocalHost = true };
        var hull = ShipTestHulls.Create();
        var order = new List<string>();
        harness.Rollback.Setup(r => r.Rollback(It.IsAny<NavalNpcFleetAttemptWork>()))
            .Callback<NavalNpcFleetAttemptWork>(work => order.Add("rollback:" + work.Hulls.Count));
        harness.Network.Setup(n => n.SendAll(It.IsAny<NetworkNpcFleetAttempted>())).Callback(() => order.Add("failed"));

        harness.Attempt(work =>
        {
            work.Hulls.Add(hull);
            throw new InvalidOperationException("second hull threw");
        });

        Assert.Equal(new[] { "rollback:1", "failed" }, order);
        harness.Rollback.Verify(r => r.Rollback(It.Is<NavalNpcFleetAttemptWork>(work => work.Hulls.Single() == hull)), Times.Once);
        Assert.True(harness.Spawner.AttemptFailed);
        var inspect = Newtonsoft.Json.Linq.JObject.FromObject(harness.Spawner.Inspect());
        Assert.Equal(1, (int)inspect["hullsRetracted"]);
    }

    // A failure inside the crewing hands the rollback every hull, the pulled troops and the reinforcement switch to restore, and
    // reports no AI hull to the server, with the players the share counted.
    [Fact]
    public void FailureDuringCrewing_TakesBackItsHullsAndWaitingTroops()
    {
        var harness = new Harness(BattleSideEnum.Defender) { IsLocalHost = true };
        var hulls = new[] { ShipTestHulls.Create(), ShipTestHulls.Create() };
        var troops = new[] { Mock.Of<IAgentOriginBase>(), Mock.Of<IAgentOriginBase>(), Mock.Of<IAgentOriginBase>() };
        NavalNpcFleetAttemptWork taken = null;
        harness.Rollback.Setup(r => r.Rollback(It.IsAny<NavalNpcFleetAttemptWork>()))
            .Callback<NavalNpcFleetAttemptWork>(work => taken = work);

        harness.Attempt(work =>
        {
            work.Troops.AddRange(troops);
            work.Hulls.AddRange(hulls);
            work.Players = new NavalSidePlayerFleets(new[] { "MobileParty_P" }, 3);
            work.BeginCrewing(TeamSideEnum.EnemyTeam, spawnedOnTickBefore: false);
            throw new NullReferenceException("crewing threw");
        });

        Assert.NotNull(taken);
        Assert.Equal(hulls, taken.Hulls);
        Assert.Equal(troops, taken.Troops);
        Assert.True(taken.CrewingStarted);
        Assert.Equal(TeamSideEnum.EnemyTeam, taken.CrewSide);
        Assert.False(taken.SpawnedOnTickBefore);
        harness.Network.Verify(n => n.SendAll(It.Is<NetworkNpcFleetAttempted>(sent => sent.Failed)), Times.Once);
        harness.VerifyReported(failed: true, aiHulls: 0, playerHulls: 3, "MobileParty_P");
    }

    // A fielded fleet keeps everything it spawned, and reports its hulls and the players its share counted to the server.
    [Fact]
    public void FieldedFleet_TakesNothingBack()
    {
        var harness = new Harness(BattleSideEnum.Defender) { IsLocalHost = true };

        harness.Attempt(work =>
        {
            work.Hulls.AddRange(new[] { ShipTestHulls.Create(), ShipTestHulls.Create() });
            work.Players = new NavalSidePlayerFleets(new[] { "MobileParty_P", "MobileParty_Q" }, 4);
            return null;
        });

        harness.Rollback.Verify(r => r.Rollback(It.IsAny<NavalNpcFleetAttemptWork>()), Times.Never);
        harness.Network.Verify(n => n.SendAll(It.Is<NetworkNpcFleetAttempted>(sent => !sent.Failed)), Times.Once);
        harness.VerifyReported(failed: false, aiHulls: 2, playerHulls: 4, "MobileParty_P", "MobileParty_Q");
    }

    // A rollback that throws keeps the attempt taking back, unsettled and unsent, and every tick retries it; the attempt fails
    // once the take-back went through, and no second attempt starts meanwhile.
    [Fact]
    public void ThrowingRollback_IsRetriedEveryTick_AndSettlesFailedOnlyOnceItWentThrough()
    {
        var harness = new Harness(BattleSideEnum.Defender) { IsLocalHost = true };
        int rollbacks = 0;
        harness.Rollback.Setup(r => r.Rollback(It.IsAny<NavalNpcFleetAttemptWork>())).Callback(() =>
        {
            if (++rollbacks < 3) throw new InvalidOperationException("rollback");
        });

        harness.Attempt(work =>
        {
            work.Hulls.Add(ShipTestHulls.Create());
            throw new InvalidOperationException("crewing threw");
        });
        harness.Spawner.Tick();
        bool spawnedAgain = false;
        harness.Attempt(_ => { spawnedAgain = true; return null; });

        Assert.False(spawnedAgain);
        Assert.False(harness.Spawner.AttemptFailed);
        Assert.Equal(NavalNpcFleetSpawner.TakingBack, (string)Newtonsoft.Json.Linq.JObject.FromObject(harness.Spawner.Inspect())["state"]);
        harness.Network.Verify(n => n.SendAll(It.IsAny<NetworkNpcFleetAttempted>()), Times.Never);

        harness.Spawner.Tick();
        harness.Spawner.Tick();

        Assert.Equal(3, rollbacks);
        Assert.True(harness.Spawner.AttemptFailed);
        harness.Network.Verify(n => n.SendAll(It.Is<NetworkNpcFleetAttempted>(sent => sent.Failed)), Times.Once);
    }

    private const string Own = "own";

    // One side's spawner over a mocked session and network, with a real broker (shared by the sides of one client when given)
    // for the attempt subscription.
    private sealed class Harness
    {
        public Mock<IBattleNetwork> Network { get; } = new Mock<IBattleNetwork>();
        public Mock<INetwork> Relay { get; } = new Mock<INetwork>();
        public Mock<INavalNpcFleetRollback> Rollback { get; } = new Mock<INavalNpcFleetRollback>();
        public Mock<INavalNpcFleetFielder> Fielder { get; } = new Mock<INavalNpcFleetFielder>();
        public bool IsLocalHost { get; set; }
        public NavalNpcFleetSpawner Spawner { get; }

        public Harness(BattleSideEnum side, System.Func<bool> scenarioFailsSpawn = null, MessageBroker? broker = null)
        {
            var session = new Mock<IBattleSession>();
            session.SetupGet(s => s.IsLocalHost).Returns(() => IsLocalHost);
            session.SetupGet(s => s.InstanceId).Returns("MapEvent_1");
            session.Setup(s => s.IsOwn(It.IsAny<string>())).Returns((string id) => id == Own);
            Spawner = new NavalNpcFleetSpawner(Network.Object, Relay.Object, broker ?? new MessageBroker(), session.Object,
                Mock.Of<IBattleShipReplicator>(), side, (_, _) => Fielder.Object, _ => Rollback.Object
#if DEBUG
                , ScenarioState(scenarioFailsSpawn)
#endif
                );
        }

        // The one attempt sent to the server: this battle's, with what it left and counted.
        public void VerifyReported(bool failed, int aiHulls, int playerHulls, params string[] countedPartyIds) =>
            Relay.Verify(relay => relay.SendAll(It.Is<NetworkNpcFleetAttempted>(sent => sent.InstanceId == "MapEvent_1"
                && sent.Failed == failed && sent.AiHulls == aiHulls && sent.PlayerHulls == playerHulls
                && sent.CountedPartyIds.SequenceEqual(countedPartyIds))), Times.Once);

        // Makes one attempt whose fielder does what the given step does with the attempt's work.
        public void Attempt(System.Func<NavalNpcFleetAttemptWork, string> field)
        {
            Fielder.Setup(f => f.Field(It.IsAny<Mission>(), It.IsAny<BattleSideEnum>(), It.IsAny<NavalNpcReserve>(),
                    It.IsAny<NavalNpcFleetAttemptWork>()))
                .Returns<Mission, BattleSideEnum, NavalNpcReserve, NavalNpcFleetAttemptWork>((_, _, _, work) => field(work));
            Spawner.AttemptFleet(null, default);
        }

#if DEBUG
        // A scenario of this battle that fails its AI fleet whenever the check says so, read at each attempt.
        private static INavalScenarioState ScenarioState(System.Func<bool> failsSpawn)
        {
            var scenario = new Mock<INavalScenarioState>();
            scenario.SetupGet(s => s.Current).Returns(() => failsSpawn?.Invoke() == true
                ? new NavalScenarioConfig { MapEventId = "MapEvent_1", FailNpcFleet = true }
                : null);
            return scenario.Object;
        }
#endif
    }
}
#endif
