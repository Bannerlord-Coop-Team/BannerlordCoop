#if DEBUG
using Common;
using Common.Messaging;
using Common.PacketHandlers;
using Common.Tests.Utils;
using Coop.Tests.Mocks;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using GameInterface.Services.ObjectManager;
using Missions;
using Missions.Agents;
using Missions.Agents.Handlers;
using Missions.Battles;
using Missions.Messages;
using Missions.Services.Network;
using Moq;
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Coop.Tests.Missions.Battles;

[Collection(nameof(ModInformationRoleCollection))]
public class NavalLabControllerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DelayedPeerIntroduction_ExemptsOnlyThisLabHandlerWithoutEnablingBattlePatches(bool lab)
    {
        using var broker = new TestMessageBroker();
        var registry = new Mock<INetworkAgentRegistry>(MockBehavior.Strict);
        var own = Mock.Of<IControllerIdProvider>(value => value.ControllerId == "A");
        using var handler = new AgentMovementHandler(Mock.Of<IBattleNetwork>(), Mock.Of<IPacketManager>(), broker,
            registry.Object, own, Mock.Of<IAgentEquipmentApplier>(), Mock.Of<IMovementBatchSender>(),
            Mock.Of<IPuppetMountStateRepairer>(), Mock.Of<IAgentVisualActionAccessor>(),
            Mock.Of<IMovementRateController>(), Mock.Of<IMovementPriorityScheduler>(), Mock.Of<IMissionContext>());
        if (lab) handler.ConfigureNavalLab();
        else registry.Setup(value => value.GetAgents("B")).Returns(Array.Empty<CoopAgentInfo>());
        // In the lab even enumerating the stale-party sweep fails the strict registry.
        broker.Publish(this, new NetworkMissionPeerEntered("B", "naval-lab:delayed"));
        Assert.False(BattleSpawnGate.IsCoopBattleActive);
        registry.Verify(value => value.GetAgents("B"), lab ? Times.Never() : Times.Once());
        registry.VerifyNoOtherCalls();
    }

    [Fact]
    public void FailedOpen_RollsBackNetworkMembershipAndEveryControllerSubscription()
    {
        using var broker = new TestMessageBroker();
        var relay = new TestNetwork();
        var peer = relay.CreatePeer();
        var mesh = new Mock<IBattleNetwork>();
        var component = new Mock<ICoopMissionComponent> { DefaultValue = DefaultValue.Mock };
        var context = new Mock<IMissionContext>();
        var adapter = new Mock<INavalMissionAdapter>();
        adapter.Setup(value => value.Open(It.IsAny<NavalLabManifest>(), It.IsAny<TaleWorlds.MountAndBlade.MissionBehavior>(), "A"))
            .Throws(new InvalidOperationException("native open failed"));
        var own = Mock.Of<IControllerIdProvider>(value => value.ControllerId == "A");
        using var controller = new NavalLabController(mesh.Object, relay, broker, Mock.Of<IObjectManager>(),
            component.Object, own, Mock.Of<IBattleHostRegistry>(), context.Object);
        var id = Guid.NewGuid();
        var manifest = new NavalLabManifest("naval-lab:" + id.ToString("N"), id, new[] { "A", "B" },
            Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray(), new[] { Guid.NewGuid(), Guid.NewGuid() }, NavalLabMode.TwoClientNative);
        Assert.Throws<InvalidOperationException>(() => controller.Start(manifest, adapter.Object, () => { }));
        controller.AbortStart();
        controller.AbortStart();
        mesh.Verify(value => value.Stop(), Times.Once);
        context.Verify(value => value.EndInstance(), Times.Once);
        Assert.Single(relay.GetPeerMessagesFromType<NetworkMissionEntered>(peer));
        Assert.Single(relay.GetPeerMessagesFromType<NetworkMissionLeft>(peer));
        Assert.Equal(0, broker.GetTotalSubscribers());
        component.Verify(value => value.AgentMovementHandler.ConfigureNavalLab(), Times.Once);
        component.Verify(value => value.AgentMovementHandler.Dispose(), Times.AtLeastOnce);
        component.Verify(value => value.AgentActionHandler.Dispose(), Times.AtLeastOnce);
    }
}
#endif
