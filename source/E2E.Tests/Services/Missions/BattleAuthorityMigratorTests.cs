using Common;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.Players;
using Missions;
using Missions.Battles;
using Missions.Messages;
using Missions.Services.Network;
using Moq;
using System;
using TaleWorlds.Core;
using Xunit;

namespace E2E.Tests.Services.Missions;

public class BattleAuthorityMigratorTests
{
    [Fact]
    public void NonHostDisconnect_PreparesReserveRecovery()
    {
        const string mapEventId = "battle";
        using var broker = new MessageBroker();
        var relay = new Mock<INetwork>();
        var session = new Mock<IBattleSession>();
        session.SetupGet(value => value.InstanceId).Returns(mapEventId);
        session.SetupGet(value => value.HasInstance).Returns(true);
        session.SetupGet(value => value.IsLocalHost).Returns(true);
        session.SetupGet(value => value.OwnControllerId).Returns("host");
        session.Setup(value => value.IsHostController("dropped")).Returns(false);
        session.Setup(value => value.IsOwn("dropped")).Returns(false);

        var registry = new Mock<INetworkAgentRegistry>();
        registry.Setup(value => value.GetAgents("dropped")).Returns(Array.Empty<CoopAgentInfo>());
        var mission = new Mock<ICoopMissionComponent>();
        mission.SetupGet(value => value.AgentRegistry).Returns(registry.Object);

        var reinforcementFielder = new Mock<IReinforcementFielder>();
        using var migrator = new BattleAuthorityMigrator(
            relay.Object,
            broker,
            Mock.Of<IObjectManager>(),
            Mock.Of<IPlayerManager>(),
            mission.Object,
            session.Object,
            Mock.Of<ICasualtyAttributionMap>(),
            Mock.Of<IBattleDeploymentCoordinator>(),
            Mock.Of<IAgentFormationAssigner>(),
            Mock.Of<IMissionContext>(),
            reinforcementFielder.Object,
            Mock.Of<IBattleInstanceLifecycle>(),
            Mock.Of<IBattleResultCommitter>(),
            new BattleAgentFader());

        broker.Publish(this, new MissionPeerDisconnected("dropped", mapEventId));

        reinforcementFielder.Verify(value => value.PrepareForReserveOwnershipExpansion(), Times.Once);
    }

    // R41a-2: at sea the ship replicator owns a departure and withdraws the party after the leaver's hulls are gone, so the
    // migrator's own leave handling touches no agent; on land it withdraws the party at once.
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void PeerLeft_WithdrawsThePartyAtOnceOnlyOnLand(bool isNaval, bool expectWithdrawal)
    {
        const string mapEventId = "battle";
        using var broker = new MessageBroker();
        var session = new Mock<IBattleSession>();
        session.SetupGet(value => value.InstanceId).Returns(mapEventId);
        session.SetupGet(value => value.HasInstance).Returns(true);
        session.SetupGet(value => value.OwnControllerId).Returns("host");
        session.SetupGet(value => value.IsNaval).Returns(isNaval);

        var registry = new Mock<INetworkAgentRegistry>();
        registry.Setup(value => value.GetControllerIds()).Returns(new[] { "left" });
        registry.Setup(value => value.GetAgents(It.IsAny<string>())).Returns(Array.Empty<CoopAgentInfo>());
        var mission = new Mock<ICoopMissionComponent>();
        mission.SetupGet(value => value.AgentRegistry).Returns(registry.Object);

        using var migrator = new BattleAuthorityMigrator(
            Mock.Of<INetwork>(),
            broker,
            Mock.Of<IObjectManager>(),
            Mock.Of<IPlayerManager>(),
            mission.Object,
            session.Object,
            Mock.Of<ICasualtyAttributionMap>(),
            Mock.Of<IBattleDeploymentCoordinator>(),
            Mock.Of<IAgentFormationAssigner>(),
            Mock.Of<IMissionContext>(),
            Mock.Of<IReinforcementFielder>(),
            Mock.Of<IBattleInstanceLifecycle>(),
            Mock.Of<IBattleResultCommitter>(),
            new BattleAgentFader());

        // The withdrawal runs on the game thread; this thread stands in for it, and the previous registration goes back.
        int previousGameThread = GameThread.Instance.GameThreadId;
        GameThread.Instance.MarkGameThread();
        try
        {
            broker.Publish(this, new MissionPeerLeft("left", mapEventId));
        }
        finally
        {
            GameThread.Instance.RestoreGameThread(previousGameThread);
        }

        Assert.Equal(expectWithdrawal, BattleAuthorityMigrator.WithdrawsPartyOnDeparture(isNaval));
        registry.Verify(value => value.GetControllerIds(), expectWithdrawal ? Times.Once() : Times.Never());
    }

    [Theory]
    [InlineData(true, false, 1)]
    [InlineData(true, true, 0)]
    [InlineData(false, false, 1)]
    [InlineData(false, true, 1)]
    public void PromotionToThisClient_AdoptsUnlessANavalResultIsHeld(bool isNaval, bool resultHeld, int expectedPromotions)
    {
        const string mapEventId = "battle";
        using var broker = new MessageBroker();
        var session = new Mock<IBattleSession>();
        session.SetupGet(value => value.InstanceId).Returns(mapEventId);
        session.SetupGet(value => value.OwnControllerId).Returns("peer");
        session.Setup(value => value.IsOwn("peer")).Returns(true);
        session.SetupGet(value => value.IsNaval).Returns(isNaval);

        var registry = new Mock<INetworkAgentRegistry>();
        registry.Setup(value => value.GetAgents(It.IsAny<string>())).Returns(Array.Empty<CoopAgentInfo>());
        registry.Setup(value => value.GetControllerIds()).Returns(Array.Empty<string>());
        var mission = new Mock<ICoopMissionComponent>();
        mission.SetupGet(value => value.AgentRegistry).Returns(registry.Object);
        var missionContext = new Mock<IMissionContext>();
        missionContext.SetupGet(value => value.ControllersInMission).Returns(Array.Empty<string>());

        var resultCommitter = new Mock<IBattleResultCommitter>();
        var resolvedState = BattleState.DefenderVictory;
        resultCommitter.Setup(value => value.TryGetResolvedState(out resolvedState)).Returns(resultHeld);
        var deployment = new Mock<IBattleDeploymentCoordinator>();

        using var migrator = new BattleAuthorityMigrator(
            Mock.Of<INetwork>(),
            broker,
            Mock.Of<IObjectManager>(),
            Mock.Of<IPlayerManager>(),
            mission.Object,
            session.Object,
            Mock.Of<ICasualtyAttributionMap>(),
            deployment.Object,
            Mock.Of<IAgentFormationAssigner>(),
            missionContext.Object,
            Mock.Of<IReinforcementFielder>(),
            Mock.Of<IBattleInstanceLifecycle>(),
            resultCommitter.Object,
            new BattleAgentFader());

        broker.Publish(this, new BattleHostMigrated(mapEventId, "host", "peer"));

        deployment.Verify(value => value.OnPromotedToHost(), Times.Exactly(expectedPromotions));
    }
}
