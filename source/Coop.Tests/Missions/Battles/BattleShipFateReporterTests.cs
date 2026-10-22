using Common.Messaging;
using Common.Network;
using GameInterface.Services.Ships.Messages;
using Missions;
using Missions.Battles;
using Missions.Messages;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using Xunit;

namespace Coop.Tests.Missions.Battles;

/// <summary>M6 D2/D3: when the client simulating a hull reports its campaign ship's fate to the server.</summary>
public class BattleShipFateReporterTests
{
    private const string Own = "own";
    private const string Host = "host";

    [Fact]
    public void Tick_SendsASinkingHullAtOnce_WithoutWaitingForTheInterval()
    {
        var harness = new Harness();
        var hull = harness.Field(Own, "Ship_1");
        harness.Reporter.Tick(0.1f);
        Assert.Empty(harness.Sent);

        harness.Conditions[hull] = Condition(0f, BattleShipCondition.Sinking);
        harness.Reporter.Tick(0.1f);

        var fate = Assert.Single(Assert.Single(harness.Sent).Fates);
        Assert.Equal("Ship_1", fate.CampaignShipId);
        Assert.True(fate.Sunk);
    }

    [Fact]
    public void Tick_CoalescesHitPointChangesOverTheInterval_AndSendsOnlyChangedHulls()
    {
        var harness = new Harness();
        var first = harness.Field(Own, "Ship_1");
        var second = harness.Field(Own, "Ship_2");

        harness.Reporter.Tick(BattleShipFateReporter.ReportIntervalSeconds);
        Assert.Equal(new[] { "Ship_1", "Ship_2" }, Assert.Single(harness.Sent).Fates.Select(fate => fate.CampaignShipId));

        harness.Conditions[first] = Condition(700f);
        harness.Reporter.Tick(0.5f);
        harness.Conditions[first] = Condition(650f);
        harness.Reporter.Tick(1f);
        Assert.Single(harness.Sent);

        harness.Reporter.Tick(0.5f);

        var update = harness.Sent.Last();
        var fate = Assert.Single(update.Fates);
        Assert.Equal("Ship_1", fate.CampaignShipId);
        Assert.Equal(650f, fate.HitPoints);
        Assert.False(fate.Sunk);
        Assert.Equal(2, update.Revision);
        Assert.Equal(2, harness.Reporter.FatesSentOf(harness.ShipIdOf(first)));
        Assert.Equal(1, harness.Reporter.FatesSentOf(harness.ShipIdOf(second)));
    }

    [Fact]
    public void Tick_NeverSendsASunkShipAgain()
    {
        var harness = new Harness();
        var hull = harness.Field(Own, "Ship_1");
        harness.Conditions[hull] = Condition(0f, BattleShipCondition.Sunk);
        harness.Reporter.Tick(0.1f);

        harness.Conditions[hull] = Condition(0f, BattleShipCondition.Sunk, sailHitPoints: 10f);
        harness.Reporter.Tick(BattleShipFateReporter.ReportIntervalSeconds);
        harness.Reporter.Flush();

        Assert.Single(harness.Sent);
    }

    [Fact]
    public void ReportRemoval_SendsTheLeavingHullAtOnce_AndOnlyIfItChanged()
    {
        var harness = new Harness();
        var leaving = harness.Field(Own, "Ship_1");
        harness.Field(Own, "Ship_2");
        harness.Conditions[leaving] = Condition(400f);

        harness.Reporter.ReportRemoval(leaving);
        harness.Reporter.ReportRemoval(leaving);

        var fate = Assert.Single(Assert.Single(harness.Sent).Fates);
        Assert.Equal("Ship_1", fate.CampaignShipId);
        Assert.Equal(400f, fate.HitPoints);
    }

    // The committer runs the reporter's flush before its result goes out, so both ride the ordered relay channel in that order.
    [Fact]
    public void ResultReport_FlushesTheFatesAheadOfTheResult()
    {
        var harness = new Harness();
        harness.Field(Own, "Ship_1");
        var committer = new BattleResultCommitter(Mock.Of<IBattleNetwork>(), harness.Network.Object, harness.Session.Object);
        committer.SetBeforeReport(harness.Reporter.Flush);

        committer.AcceptResolvedState(BattleState.AttackerVictory);
        committer.ReportAcceptedResult();

        Assert.IsType<NetworkBattleShipFates>(harness.Messages[0]);
        Assert.IsType<NetworkBattleResultReady>(harness.Messages[1]);
        Assert.Equal(2, harness.Messages.Count);
    }

    [Fact]
    public void Reports_OnlyTheHullsThisClientSimulates_AndHullsStandingForACampaignShip()
    {
        var harness = new Harness();
        harness.Field("other", "Ship_1");
        harness.Field(Own, null);
        harness.Field(Own, "Ship_3");

        harness.Reporter.Flush();

        Assert.Equal("Ship_3", Assert.Single(Assert.Single(harness.Sent).Fates).CampaignShipId);
    }

    // An AI hull follows the battle host: only the host reports it, at the host epoch it holds.
    [Fact]
    public void AiHulls_AreReportedByTheBattleHostOnly_AtItsEpoch()
    {
        var onHost = new Harness(Host, hostEpoch: 3);
        onHost.Field(Host, "Ship_pirate", isNpc: true);
        var onPeer = new Harness(Own, hostEpoch: 3);
        onPeer.Field(Host, "Ship_pirate", isNpc: true);

        onHost.Reporter.Flush();
        onPeer.Reporter.Flush();

        var sent = Assert.Single(onHost.Sent);
        Assert.Equal(3, sent.HostEpoch);
        Assert.Equal("battle", sent.InstanceId);
        Assert.Empty(onPeer.Sent);
    }

    [Fact]
    public void Flush_SkipsAHullThatAlreadyLeftTheMission()
    {
        var harness = new Harness();
        var hull = harness.Field(Own, "Ship_1");
        harness.Removed.Add(hull);

        harness.Reporter.Flush();

        Assert.Empty(harness.Sent);
    }

    // M6 D7: each fate carries the damage mitigated since the last one, so the server's sum is the battle's total once.
    [Fact]
    public void Fates_CarryTheMitigatedDamageSinceTheLastFate()
    {
        var harness = new Harness();
        var hull = harness.Field(Own, "Ship_1");
        harness.Mitigated[hull] = 30f;
        harness.Reporter.Flush();
        harness.Mitigated[hull] = 50f;
        harness.Reporter.Flush();
        harness.Reporter.Flush();

        Assert.Equal(new[] { 30f, 20f }, harness.Sent.Select(sent => Assert.Single(sent.Fates).MitigatedDamage));
        Assert.All(harness.Sent, sent => Assert.Equal(900f, sent.Fates[0].HitPoints));
    }

    [Theory]
    [InlineData(500f, 0, false)]
    [InlineData(500f, BattleShipCondition.Sinking, true)]
    [InlineData(500f, BattleShipCondition.Sunk, true)]
    [InlineData(0f, 0, true)]
    public void FateOf_CountsASinkingHullOrOneAtZeroHitPointsAsSunk(float hitPoints, int sinkingState, bool sunk)
    {
        var fate = BattleShipFateReporter.FateOf("Ship_1", Condition(hitPoints, sinkingState));

        Assert.Equal(sunk, fate.Sunk);
        Assert.Equal(hitPoints, fate.HitPoints);
    }

    private static BattleShipCondition Condition(float hitPoints, int sinkingState = 0, float sailHitPoints = 300f) =>
        new BattleShipCondition(hitPoints, sailHitPoints, 0f, null, sinkingState);

    // A reporter over a real ship registry, a mocked engine whose conditions the test sets and a relay that records sends.
    private sealed class Harness
    {
        private readonly NetworkShipRegistry registry = new NetworkShipRegistry();
        private readonly Dictionary<MissionObject, string> campaignShips = new Dictionary<MissionObject, string>();

        public Harness(string ownControllerId = Own, int hostEpoch = 1)
        {
            Session.SetupGet(session => session.HasInstance).Returns(true);
            Session.SetupGet(session => session.InstanceId).Returns("battle");
            Session.SetupGet(session => session.OwnControllerId).Returns(ownControllerId);
            Session.SetupGet(session => session.HostEpoch).Returns(hostEpoch);
            Session.Setup(session => session.IsOwn(It.IsAny<string>())).Returns<string>(id => id == ownControllerId);
            Engine.Setup(engine => engine.ReadCondition(It.IsAny<MissionObject>()))
                .Returns<MissionObject>(hull => Conditions[hull]);
            Engine.Setup(engine => engine.CampaignShipIdOf(It.IsAny<MissionObject>()))
                .Returns<MissionObject>(hull => campaignShips[hull]);
            Engine.Setup(engine => engine.IsHullRemoved(It.IsAny<MissionObject>())).Returns<MissionObject>(Removed.Contains);
            Engine.Setup(engine => engine.ReadMitigatedDamage(It.IsAny<MissionObject>()))
                .Returns<MissionObject>(hull => Mitigated.TryGetValue(hull, out var damage) ? damage : 0f);
            Network.Setup(network => network.SendAll(It.IsAny<IMessage>())).Callback<IMessage>(Messages.Add);
            Reporter = new BattleShipFateReporter(Network.Object, Session.Object, registry, Engine.Object);
        }

        public Mock<IBattleSession> Session { get; } = new Mock<IBattleSession>();
        public Mock<INavalShipEngine> Engine { get; } = new Mock<INavalShipEngine>();
        public Mock<INetwork> Network { get; } = new Mock<INetwork>();
        public BattleShipFateReporter Reporter { get; }
        public Dictionary<MissionObject, BattleShipCondition> Conditions { get; } = new Dictionary<MissionObject, BattleShipCondition>();
        public HashSet<MissionObject> Removed { get; } = new HashSet<MissionObject>();
        public Dictionary<MissionObject, float> Mitigated { get; } = new Dictionary<MissionObject, float>();
        public List<IMessage> Messages { get; } = new List<IMessage>();
        public List<NetworkBattleShipFates> Sent => Messages.OfType<NetworkBattleShipFates>().ToList();

        // A registered hull at full condition, simulated by authority and standing for a campaign ship when given one.
        public MissionObject Field(string authority, string campaignShipId, bool isNpc = false)
        {
            var hull = ShipTestHulls.Create();
            campaignShips[hull] = campaignShipId;
            Conditions[hull] = Condition(900f);
            Assert.True(registry.TryRegister(new NetworkShipInfo(Guid.NewGuid(), authority, "MapEventParty_1", isNpc, hull, null)));
            return hull;
        }

        public Guid ShipIdOf(MissionObject hull) => registry.TryGetByHull(hull, out var ship) ? ship.ShipId : Guid.Empty;
    }
}
