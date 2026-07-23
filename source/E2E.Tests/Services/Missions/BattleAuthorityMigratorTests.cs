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
            reinforcementFielder.Object);

        broker.Publish(this, new MissionPeerDisconnected("dropped", mapEventId));

        reinforcementFielder.Verify(value => value.PrepareForReserveOwnershipExpansion(), Times.Once);
    }
}
