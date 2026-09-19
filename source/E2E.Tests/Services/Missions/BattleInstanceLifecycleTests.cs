using System;
using System.Collections.Generic;
using System.Linq;
using Common.Messaging;
using Common.Network;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using Missions;
using Missions.Agents;
using Missions.Battles;
using Missions.Messages;
using Missions.Services.Network;
using Moq;
using TaleWorlds.MountAndBlade;
using Xunit;
using Xunit.Abstractions;

namespace E2E.Tests.Services.Missions;

public class BattleInstanceLifecycleTests : MissionTestEnvironment
{
    public BattleInstanceLifecycleTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    [Trait("Requirement", "BR-054")]
    public void Leave_ClearsLocalMissionMembershipBeforeReentry()
    {
        var (mapEventId, _) = SetupCoopBattle("A", "B");
        var client = Clients.First();

        client.Call(() =>
        {
            var broker = client.Resolve<IMessageBroker>();
            var context = client.Resolve<IMissionContext>();
            broker.Publish(this, new NetworkMissionPeerEntered("B", mapEventId));
            Assert.Contains("B", context.ControllersInMission);

            var session = new BattleSession(
                client.Resolve<IControllerIdProvider>(),
                client.Resolve<IBattleHostRegistry>());
            session.TryBegin(mapEventId);
            var worldItemRegistry = new RecordingWorldItemRegistry();
            var agentRegistry = new Mock<INetworkAgentRegistry>();
            var missionComponent = new Mock<ICoopMissionComponent>();
            missionComponent.SetupGet(component => component.AgentRegistry).Returns(agentRegistry.Object);

            using var lifecycle = new BattleInstanceLifecycle(
                client.Resolve<IBattleNetwork>(),
                client.Resolve<INetwork>(),
                broker,
                objectManager: null,
                coopMissionComponent: missionComponent.Object,
                worldItemRegistry: worldItemRegistry,
                session: session,
                missionContext: context);

            lifecycle.Leave(wasRetreat: false);

            Assert.Empty(context.ControllersInMission);
            agentRegistry.Verify(registry => registry.Clear(), Times.Once);
            Assert.Equal(1, worldItemRegistry.ClearCalls);
        });
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public void Leave_SendsRetreatSignalOnlyForUnresolvedRetreat(bool wasRetreat, int expectedRetreatMessages)
    {
        var (mapEventId, _) = SetupCoopBattle("A", "B");
        var client = Clients.First();

        client.Call(() =>
        {
            var session = new BattleSession(
                client.Resolve<IControllerIdProvider>(),
                client.Resolve<IBattleHostRegistry>());
            Assert.True(session.TryBegin(mapEventId));
            var agentRegistry = new Mock<INetworkAgentRegistry>();
            var missionComponent = new Mock<ICoopMissionComponent>();
            missionComponent.SetupGet(component => component.AgentRegistry).Returns(agentRegistry.Object);

            using var lifecycle = new BattleInstanceLifecycle(
                client.Resolve<IBattleNetwork>(),
                client.Resolve<INetwork>(),
                client.Resolve<IMessageBroker>(),
                objectManager: null,
                coopMissionComponent: missionComponent.Object,
                worldItemRegistry: new RecordingWorldItemRegistry(),
                session: session,
                missionContext: client.Resolve<IMissionContext>());

            lifecycle.Leave(wasRetreat);

            Assert.Equal(expectedRetreatMessages,
                client.NetworkSentMessages.GetMessageCount<NetworkBattleRetreated>());
            Assert.Equal(1, client.NetworkSentMessages.GetMessageCount<NetworkMissionLeft>());
            agentRegistry.Verify(registry => registry.Clear(), Times.Once);
        });
    }

    private sealed class RecordingWorldItemRegistry : INetworkWorldItemRegistry
    {
        public int ClearCalls { get; private set; }

        public Guid GetOrCreateId(SpawnedItemEntity item) => throw new NotSupportedException();
        public bool TryGetId(SpawnedItemEntity item, out Guid itemId) => throw new NotSupportedException();
        public void Register(Guid itemId, SpawnedItemEntity item) => throw new NotSupportedException();
        public bool TryGet(Guid itemId, out SpawnedItemEntity item) => throw new NotSupportedException();
        public IReadOnlyDictionary<Guid, SpawnedItemEntity> GetAll() => throw new NotSupportedException();
        public void Remove(Guid itemId) => throw new NotSupportedException();

        public void Clear()
        {
            ClearCalls++;
        }
    }
}
