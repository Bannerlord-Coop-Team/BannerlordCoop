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

    [Theory]
    [InlineData(null, "failed:incomplete native crew")]
    [InlineData("System.InvalidOperationException: native init failed\n   at NativeFactory.InitForMission()", "failed:System.InvalidOperationException: native init failed\n   at NativeFactory.InitForMission()")]
    public void FailedNativeInitialization_ReportsOriginalFailureWithoutReadiness(string? blocker, string expected)
    {
        using var broker = new TestMessageBroker();
        var relay = new TestNetwork();
        var peer = relay.CreatePeer();
        var component = new Mock<ICoopMissionComponent> { DefaultValue = DefaultValue.Mock };
        var adapter = new Mock<INavalMissionAdapter>();
        adapter.SetupGet(value => value.Blocker).Returns(blocker);
        adapter.SetupGet(value => value.Agents).Returns(Array.Empty<TaleWorlds.MountAndBlade.Agent>());
        var own = Mock.Of<IControllerIdProvider>(value => value.ControllerId == "A");
        using var controller = new NavalLabController(Mock.Of<IBattleNetwork>(), relay, broker, Mock.Of<IObjectManager>(),
            component.Object, own, Mock.Of<IBattleHostRegistry>(), Mock.Of<IMissionContext>(), new NavalLabMeasurement());
        var id = Guid.NewGuid();
        var manifest = new NavalLabManifest("naval-lab:" + id.ToString("N"), id, new[] { "A", "B" },
            Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray(), new[] { Guid.NewGuid(), Guid.NewGuid() });
        int readyCount = 0;
        broker.Subscribe<BattleMissionReady>(_ => readyCount++);
        controller.Start(manifest, adapter.Object, () => { });
        controller.AfterStart();
        Assert.Equal(expected, Assert.Single(relay.GetPeerMessagesFromType<NetworkNavalLabReceipt>(peer)).Status);
        Assert.Equal(0, readyCount);
        component.Verify(value => value.AgentRegistry.Clear(), Times.Never);
        adapter.Verify(value => value.SetAuthority(true), Times.Never);
        controller.AbortStart();
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
            component.Object, own, Mock.Of<IBattleHostRegistry>(), context.Object, new NavalLabMeasurement());
        var id = Guid.NewGuid();
        var manifest = new NavalLabManifest("naval-lab:" + id.ToString("N"), id, new[] { "A", "B" },
            Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray(), new[] { Guid.NewGuid(), Guid.NewGuid() });
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
    [Fact]
    public void Probe_RejectsStaleIdentityOwnerAndEpochAndCancelsOnHold()
    {
        using var broker = new TestMessageBroker();
        var own = Mock.Of<IControllerIdProvider>(value => value.ControllerId == "B");
        var hosts = new BattleHostRegistry(own);
        var id = Guid.NewGuid();
        var manifest = new NavalLabManifest("naval-lab:" + id.ToString("N"), id, new[] { "A", "B" },
            Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray(), new[] { Guid.NewGuid(), Guid.NewGuid() });
        hosts.Set(manifest.InstanceId, new BattleHostAssignment("A", new[] { "B" }, 1));
        var adapter = new Mock<INavalMissionAdapter>();
        var component = new Mock<ICoopMissionComponent> { DefaultValue = DefaultValue.Mock };
        var measurement = new NavalLabMeasurement();
        using var controller = new NavalLabController(Mock.Of<IBattleNetwork>(), new TestNetwork(), broker,
            Mock.Of<IObjectManager>(), component.Object, own, hosts, Mock.Of<IMissionContext>(), measurement);
        controller.Start(manifest, adapter.Object, () => { });
        Assert.Equal("applied", controller.Apply(new NetworkNavalLabAction(id, Guid.NewGuid(), 1, "release", 0, 0, false)));
        Assert.Equal("rejected:stale_incarnation", controller.Apply(new NetworkNavalLabAction(Guid.NewGuid(), Guid.NewGuid(), 1, "probe", 0, 0, false)));
        Assert.Equal("rejected:stale_epoch", controller.Apply(new NetworkNavalLabAction(id, Guid.NewGuid(), 2, "probe", 0, 0, false)));
        Assert.Equal("rejected:not_original_owner", controller.Apply(new NetworkNavalLabAction(id, Guid.NewGuid(), 1, "walk", 0, 1, false)));
        Assert.Equal("rejected:invalid_control", controller.Apply(new NetworkNavalLabAction(id, Guid.NewGuid(), 1, "walk", 1, 1, true)));
        Assert.Equal("rejected:agent_authority_changed_or_unavailable", controller.Apply(new NetworkNavalLabAction(id, Guid.NewGuid(), 1, "walk", 1, 1, false)));
        var probe = new NetworkNavalLabAction(id, Guid.NewGuid(), 1, "probe", 0, 0.5f, true);
        Assert.Equal("applied", controller.Apply(probe));
        Assert.Equal("duplicate:not_restarted", controller.Apply(probe));
        Assert.Equal("held", controller.Apply(new NetworkNavalLabAction(id, Guid.NewGuid(), 0, "hold", 0, 0, false)));
        Assert.Equal("cancelled:hold", (string?)JObject.FromObject(measurement.Read(0))["status"]);
        adapter.Verify(value => value.SetHelm(It.IsAny<int>(), It.IsAny<float>(), It.IsAny<bool>()), Times.Never);
        adapter.Verify(value => value.CancelControls(), Times.Once);
        hosts.Set(manifest.InstanceId, new BattleHostAssignment("A", Array.Empty<string>(), 1));
        controller.Apply(new NetworkNavalLabAction(id, Guid.NewGuid(), 1, "release", 0, 0, false));
        Assert.Equal("rejected:not_released_or_owner_departed", controller.Apply(new NetworkNavalLabAction(id, Guid.NewGuid(), 1, "walk", 1, 1, false)));
        controller.AbortStart();
        adapter.Verify(value => value.CancelControls(), Times.AtLeast(2));
    }

    [Fact]
    public void Helm_RejectsExpiredProbeUntilCallbackCleanupThenRetainsNewInput()
    {
        using var broker = new TestMessageBroker();
        var own = Mock.Of<IControllerIdProvider>(value => value.ControllerId == "A");
        var hosts = new BattleHostRegistry(own);
        var id = Guid.NewGuid();
        var manifest = new NavalLabManifest("naval-lab:" + id.ToString("N"), id, new[] { "A", "B" },
            Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray(), new[] { Guid.NewGuid(), Guid.NewGuid() });
        hosts.Set(manifest.InstanceId, new BattleHostAssignment("A", new[] { "B" }, 1));
        var adapter = new Mock<INavalMissionAdapter>();
        var component = new Mock<ICoopMissionComponent> { DefaultValue = DefaultValue.Mock };
        var measurement = new NavalLabMeasurement();
        using var controller = new NavalLabController(Mock.Of<IBattleNetwork>(), new TestNetwork(), broker,
            Mock.Of<IObjectManager>(), component.Object, own, hosts, Mock.Of<IMissionContext>(), measurement);
        controller.Start(manifest, adapter.Object, () => { });
        Assert.Equal("applied", controller.Apply(new NetworkNavalLabAction(id, Guid.NewGuid(), 1, "release", 0, 0, false)));
        Assert.Equal("applied", controller.Apply(new NetworkNavalLabAction(id, Guid.NewGuid(), 1, "probe", 0, 0.5f, true)));
        var helm = new NetworkNavalLabAction(id, Guid.NewGuid(), 1, "helm", 1, -0.5f, true);
        Assert.Equal("rejected:probe_driving", controller.Apply(helm));
        // Expire the real window without sleeping or running the cleanup callback.
        Assert.False(measurement.Active(double.MaxValue));
        adapter.Invocations.Clear();
        Assert.Equal("rejected:probe_driving", controller.Apply(helm));
        adapter.Verify(value => value.SetHelm(It.IsAny<int>(), It.IsAny<float>(), It.IsAny<bool>()), Times.Never);

        controller.OnMissionTick(0);
        adapter.Verify(value => value.SetHelm(0, 0, false), Times.Once);
        adapter.Verify(value => value.SetHelm(1, 0, false), Times.Once);
        adapter.Verify(value => value.SetHelm(It.IsAny<int>(), It.IsAny<float>(), It.IsAny<bool>()), Times.Exactly(2));
        adapter.Invocations.Clear();
        Assert.Equal("applied", controller.Apply(helm));
        controller.OnMissionTick(0);
        adapter.Verify(value => value.SetHelm(1, -0.5f, true), Times.Once);
        adapter.Verify(value => value.SetHelm(It.IsAny<int>(), It.IsAny<float>(), It.IsAny<bool>()), Times.Once);
        controller.AbortStart();
    }

    [Fact]
    public void Frames_ReportSupersededObservationAndRejectStaleOrNonFiniteInputs()
    {
        using var broker = new TestMessageBroker();
        var own = Mock.Of<IControllerIdProvider>(value => value.ControllerId == "B");
        var hosts = new BattleHostRegistry(own);
        var id = Guid.NewGuid();
        var manifest = new NavalLabManifest("naval-lab:" + id.ToString("N"), id, new[] { "A", "B" },
            Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray(), new[] { Guid.NewGuid(), Guid.NewGuid() });
        hosts.Set(manifest.InstanceId, new BattleHostAssignment("A", new[] { "B" }, 1));
        var adapter = new Mock<INavalMissionAdapter>();
        var component = new Mock<ICoopMissionComponent> { DefaultValue = DefaultValue.Mock };
        var measurement = new NavalLabMeasurement();
        using var controller = new NavalLabController(Mock.Of<IBattleNetwork>(), new TestNetwork(), broker,
            Mock.Of<IObjectManager>(), component.Object, own, hosts, Mock.Of<IMissionContext>(), measurement);
        controller.Start(manifest, adapter.Object, () => { });
        controller.Apply(new NetworkNavalLabAction(id, Guid.NewGuid(), 1, "release", 0, 0, false));
        adapter.Setup(value => value.ApplyFrames(It.IsAny<TaleWorlds.Library.MatrixFrame[]>())).Returns(true);
        var probe = Guid.NewGuid();
        controller.Apply(new NetworkNavalLabAction(id, probe, 1, "probe", 0, 0, false));
        broker.Publish(this, new NetworkNavalLabFrames(id, 1, 10, new float[24], 100, probe));
        broker.Publish(this, new NetworkNavalLabFrames(id, 1, 12, new float[24], 102));
        broker.Publish(this, new NetworkNavalLabFrames(id, 1, 11, new float[24], 101));
        broker.Publish(this, new NetworkNavalLabFrames(Guid.NewGuid(), 1, 13, new float[24], 103));
        broker.Publish(this, new NetworkNavalLabFrames(id, 2, 13, new float[24], 103));
        var invalid = new float[24];
        invalid[0] = float.NaN;
        broker.Publish(this, new NetworkNavalLabFrames(id, 1, 13, invalid, 103));
        GameThread.Run(() => { }, blocking: true);
        var view = JObject.FromObject(controller.Samples(0));
        Assert.Equal(1, (int)view["supersededSamples"]!);
        Assert.Equal(4, (int)view["rejectedFrames"]!);
        Assert.Equal(10, (int)view["receivedGaps"]!);
        var sample = view["measurement"]!["samples"]![0]!;
        Assert.Equal(100, (int)sample["hostSampleCallback"]!);
        Assert.Equal(JTokenType.Null, sample["observedCallback"]!.Type);
        Assert.Equal(JTokenType.Null, sample["native"]!.Type);
        adapter.Verify(value => value.ApplyFrames(It.IsAny<TaleWorlds.Library.MatrixFrame[]>()), Times.Exactly(2));
        adapter.Setup(value => value.ApplyFrames(It.IsAny<TaleWorlds.Library.MatrixFrame[]>())).Returns(false);
        broker.Publish(this, new NetworkNavalLabFrames(id, 1, 14, new float[24], 104, probe));
        GameThread.Run(() => { }, blocking: true);
        view = JObject.FromObject(controller.Samples(12));
        Assert.Equal(14, (int)view["lastReceivedSequence"]!);
        Assert.Equal(12, (int)view["lastAppliedSequence"]!);
        sample = view["measurement"]!["samples"]![0]!;
        Assert.Equal("unavailable:frames_not_applied", (string?)sample["error"]);
        Assert.Equal(JTokenType.Null, sample["appliedCallback"]!.Type);
        Assert.Equal(JTokenType.Null, sample["observedCallback"]!.Type);
        controller.AbortStart();
    }

}
#endif
