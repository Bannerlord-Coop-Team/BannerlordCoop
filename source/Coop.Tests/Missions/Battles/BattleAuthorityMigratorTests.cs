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
using Xunit;

namespace Coop.Tests.Missions.Battles;

public class BattleAuthorityMigratorTests
{
    private const string MapEventId = "battle";

    [Fact]
    public void NonHostDisconnect_PreparesReserveRecovery()
    {
        using var broker = new MessageBroker();
        var session = new Mock<IBattleSession>();
        session.SetupGet(value => value.InstanceId).Returns(MapEventId);
        session.SetupGet(value => value.HasInstance).Returns(true);
        session.SetupGet(value => value.IsLocalHost).Returns(true);
        session.SetupGet(value => value.OwnControllerId).Returns("host");
        session.Setup(value => value.IsHostController("dropped")).Returns(false);
        session.Setup(value => value.IsOwn("dropped")).Returns(false);

        var registry = new Mock<INetworkAgentRegistry>();
        registry.Setup(value => value.GetAgents("dropped")).Returns(Array.Empty<CoopAgentInfo>());
        var reinforcementFielder = new Mock<IReinforcementFielder>();
        using var migrator = CreateMigrator(broker, session, registry, reinforcementFielder: reinforcementFielder.Object);

        broker.Publish(this, new MissionPeerDisconnected("dropped", MapEventId));

        reinforcementFielder.Verify(value => value.PrepareForReserveOwnershipExpansion(), Times.Once);
    }

    // R41a-2: at sea the ship replicator owns a departure and withdraws the party after the leaver's hulls are gone, so the
    // migrator's own leave handling withdraws nothing; on land it withdraws the party at once, saying whether it hosted.
    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public void PeerLeft_WithdrawsThePartyAtOnceOnlyOnLand(bool isNaval, bool wasHost)
    {
        using var broker = new MessageBroker();
        var session = new Mock<IBattleSession>();
        session.SetupGet(value => value.InstanceId).Returns(MapEventId);
        session.SetupGet(value => value.HasInstance).Returns(true);
        session.SetupGet(value => value.OwnControllerId).Returns("own");
        session.SetupGet(value => value.IsNaval).Returns(isNaval);
        session.Setup(value => value.IsHostController("left")).Returns(wasHost);
        var partyWithdrawal = new Mock<IBattlePartyWithdrawal>();
        using var migrator = CreateMigrator(broker, session, new Mock<INetworkAgentRegistry>(), partyWithdrawal: partyWithdrawal.Object);

        broker.Publish(this, new MissionPeerLeft("left", MapEventId));
        GameThread.Run(() => { }, blocking: true);

        partyWithdrawal.Verify(value => value.WithdrawParty("left", wasHost), isNaval ? Times.Never() : Times.Once());
        partyWithdrawal.Verify(value => value.WithdrawParty(It.IsAny<string>(), It.IsAny<bool>()), isNaval ? Times.Never() : Times.Once());
    }

    // Q4: a promotion this client receives adopts the AI on land and at sea; the server alone keeps a naval host that reported.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PromotionToThisClient_Adopts(bool isNaval)
    {
        using var broker = new MessageBroker();
        var session = new Mock<IBattleSession>();
        session.SetupGet(value => value.InstanceId).Returns(MapEventId);
        session.SetupGet(value => value.OwnControllerId).Returns("peer");
        session.Setup(value => value.IsOwn("peer")).Returns(true);
        session.SetupGet(value => value.IsNaval).Returns(isNaval);

        var registry = new Mock<INetworkAgentRegistry>();
        registry.Setup(value => value.GetAgents(It.IsAny<string>())).Returns(Array.Empty<CoopAgentInfo>());
        registry.Setup(value => value.GetControllerIds()).Returns(Array.Empty<string>());
        var deployment = new Mock<IBattleDeploymentCoordinator>();
        using var migrator = CreateMigrator(broker, session, registry, deployment.Object);

        broker.Publish(this, new BattleHostMigrated(MapEventId, "host", "peer"));

        deployment.Verify(value => value.OnPromotedToHost(), Times.Once);
    }

    // A migrator over the given session and agent registry, with no controller in the mission.
    private static BattleAuthorityMigrator CreateMigrator(
        MessageBroker broker,
        Mock<IBattleSession> session,
        Mock<INetworkAgentRegistry> registry,
        IBattleDeploymentCoordinator deployment = null,
        IReinforcementFielder reinforcementFielder = null,
        IBattlePartyWithdrawal partyWithdrawal = null)
    {
        var mission = new Mock<ICoopMissionComponent>();
        mission.SetupGet(value => value.AgentRegistry).Returns(registry.Object);
        var missionContext = new Mock<IMissionContext>();
        missionContext.SetupGet(value => value.ControllersInMission).Returns(Array.Empty<string>());

        return new BattleAuthorityMigrator(
            Mock.Of<INetwork>(),
            broker,
            Mock.Of<IObjectManager>(),
            Mock.Of<IPlayerManager>(),
            mission.Object,
            session.Object,
            deployment ?? Mock.Of<IBattleDeploymentCoordinator>(),
            Mock.Of<IAgentFormationAssigner>(),
            missionContext.Object,
            reinforcementFielder ?? Mock.Of<IReinforcementFielder>(),
            partyWithdrawal ?? Mock.Of<IBattlePartyWithdrawal>(),
            Mock.Of<IBattlePlayerPartyResolver>());
    }
}
