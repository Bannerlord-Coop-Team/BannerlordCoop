#if DEBUG
using Common;
using Common.Logging;
using System.Collections.Concurrent;
using Common.Messaging;
using Common.Tests.Utils;
using Coop.Tests.Mocks;
using GameInterface.Services.Entity;
using GameInterface.Services.MapEvents;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Missions.Battles;
using Missions.Messages;
using Moq;
using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Coop.Tests.Missions.Battles;

[Collection(nameof(ModInformationRoleCollection))]
public sealed class NavalLabCoordinatorTests : IDisposable
{
    private readonly bool previousRole = ModInformation.IsServer;
    private readonly string previousCapability = ModInformation.NavalLabCapability;
    private readonly TestMessageBroker broker = new();
    private readonly TestNetwork network = new();
    private readonly NavalLabSessionStore store = new();
    private readonly Mock<INavalMissionAdapter> adapter = new();
    private readonly Mock<INavalMissionAdapterLoader> loader = new();
    private readonly Mock<INavalLabController> controller = new();
    private readonly Mock<IPlayerManager> players = new();
    private readonly NavalLabManifest manifest;
    private readonly NavalLabCoordinator coordinator;
    private int factoryCalls;

    public NavalLabCoordinatorTests()
    {
        ModInformation.ConfigureNavalLab("new-campaign:571cda18-4f3b-4787-9ac0-5997f17f083e", "regression", true);
        ModInformation.IsServer = false;
        var id = Guid.NewGuid();
        manifest = new NavalLabManifest("naval-lab:" + id.ToString("N"), id, new[] { "A", "B" },
            Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray(), new[] { Guid.NewGuid(), Guid.NewGuid() });
        loader.Setup(value => value.Load()).Returns(adapter.Object);
        var own = Mock.Of<IControllerIdProvider>(value => value.ControllerId == "A");
        coordinator = new NavalLabCoordinator(broker, network, store, players.Object,
            Mock.Of<IBattleHostRegistry>(), own, loader.Object, () => { factoryCalls++; return controller.Object; }, new NavalLabNativeState());
    }

    private void Start()
    {
        broker.Publish(this, new NetworkNavalLabStart(manifest.InstanceId, manifest.IncarnationId,
            manifest.Controllers, manifest.Combatants, manifest.Ships));
        GameThread.Run(() => { }, blocking: true);
    }

    [Theory]
    [InlineData("existing mission")]
    [InlineData("missing hull or troop")]
    public void Start_PreflightFailureDoesNotConstructControllerOrChangeMembership(string reason)
    {
        var peer = network.CreatePeer();
        adapter.Setup(value => value.Preflight()).Throws(new InvalidOperationException(reason));
        Start();
        Assert.Null(store.Current);
        Assert.Equal(0, factoryCalls);
        Assert.StartsWith("failed:", Assert.Single(network.GetPeerMessagesFromType<NetworkNavalLabReceipt>(peer)).Status);
        Assert.Empty(network.GetPeerMessagesFromType<NetworkMissionEntered>(peer));
        loader.Verify(value => value.Dispose(), Times.Once);
    }

    [Fact]
    public void Start_LaterFailureAbortsControllerAndDisposesAdapter()
    {
        network.CreatePeer();
        controller.Setup(value => value.Start(It.IsAny<NavalLabManifest>(), adapter.Object, It.IsAny<Action>()))
            .Throws(new InvalidOperationException("open failed"));
        Start();
        controller.Verify(value => value.AbortStart(), Times.Once);
        loader.Verify(value => value.Dispose(), Times.Once);
        Start();
        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public void FailedOpen_PreservesStackAndDiagnosticSnapshotBeforeRollback()
    {
        var peer = network.CreatePeer();
        object? snapshot = new { phase = "init_for_mission_failed", sceneShips = new[] { "partial_drakkar" } };
        adapter.SetupGet(value => value.StartupDiagnostics).Returns(() => snapshot!);
        loader.Setup(value => value.Dispose()).Callback(() => snapshot = null);
        controller.Setup(value => value.Start(It.IsAny<NavalLabManifest>(), adapter.Object, It.IsAny<Action>()))
            .Throws(new InvalidOperationException("original factory error"));
        Start();
        var receipt = Assert.Single(network.GetPeerMessagesFromType<NetworkNavalLabReceipt>(peer));
        Assert.Contains("System.InvalidOperationException: original factory error", receipt.Status);
        Assert.Contains(" at ", receipt.Status);
        var inspection = JObject.FromObject(coordinator.Inspect());
        Assert.Equal("init_for_mission_failed", (string?)inspection["nativeStartup"]?["phase"]);
        Assert.Equal(JTokenType.Null, inspection["native"]!.Type);
    }

    [Fact]
    public void Start_EmitsConfiguredDiagnosticsBeforeNativeEntryAndRollback()
    {
        var messages = new ConcurrentQueue<string>();
        bool observedBeforeRollback = false;
        Action<string> capture = message =>
        {
            if (message.Contains("[NavalLabStartup]") && message.Contains(manifest.IncarnationId.ToString()))
                messages.Enqueue(message);
        };
        OutputSinkManager.AddLogCallback(capture);
        try
        {
            controller.Setup(value => value.Start(It.IsAny<NavalLabManifest>(), adapter.Object, It.IsAny<Action>()))
                .Callback(() =>
                {
                    var opening = Assert.Single(messages);
                    Assert.Contains("open begin", opening);
                    Assert.Contains(typeof(NavalLabCoordinator).Assembly.ManifestModule.ModuleVersionId.ToString(), opening);
                    Assert.Contains(adapter.Object.GetType().Assembly.ManifestModule.ModuleVersionId.ToString(), opening);
                    throw new InvalidOperationException("original startup failure");
                });
            controller.Setup(value => value.AbortStart()).Callback(() =>
            {
                Assert.Equal(2, messages.Count);
                Assert.Contains("open failed before rollback", messages.Last());
                Assert.Contains("System.InvalidOperationException: original startup failure", messages.Last());
                Assert.Contains(" at ", messages.Last());
                observedBeforeRollback = true;
            });
            Start();
            controller.Verify(value => value.AbortStart(), Times.Once);
            Assert.True(observedBeforeRollback);
            Assert.Equal(2, messages.Count);
            Assert.Contains("original startup failure", messages.Last());
        }
        finally
        {
            OutputSinkManager.RemoveLogCallback(capture);
        }
    }

    [Fact]
    public void FinalizedMission_RetainsFailureAndCachedInventoryAfterAdapterDisposal()
    {
        Action? onEnd = null;
        object? snapshot = new { phase = "fixture_failed", blocker = "native init failed" };
        adapter.SetupGet(value => value.StartupDiagnostics).Returns(() => snapshot!);
        adapter.SetupGet(value => value.Blocker).Returns("native init failed");
        loader.Setup(value => value.Dispose()).Callback(() => snapshot = null);
        controller.Setup(value => value.Start(It.IsAny<NavalLabManifest>(), adapter.Object, It.IsAny<Action>()))
            .Callback<NavalLabManifest, INavalMissionAdapter, Action>((_, _, end) => onEnd = end);
        Start();
        Assert.NotNull(onEnd);
        onEnd!();
        var inspection = JObject.FromObject(coordinator.Inspect());
        Assert.Equal("fixture_failed", (string?)inspection["nativeStartup"]?["phase"]);
        Assert.Equal("native init failed", (string?)inspection["failure"]);
        loader.Verify(value => value.Dispose(), Times.Once);
    }

    [Fact]
    public void FaultAndStop_AfterOrdinaryBudgetExhaustionStillReachBothPeersOnce()
    {
        ModInformation.IsServer = true;
        store.Install(manifest);
        var first = network.CreatePeer();
        var second = network.CreatePeer();
        var player = new Player("A", "", "", "", "");
        players.Setup(value => value.TryGetPeer("A", out first)).Returns(true);
        players.Setup(value => value.TryGetPeer("B", out second)).Returns(true);
        players.Setup(value => value.TryGetPlayer(first, out player)).Returns(true);
        for (int i = 0; i < 64; i++) store.BeginOperation(Guid.NewGuid(), "helm");
        broker.Publish(first, new NetworkNavalLabFault(manifest.IncarnationId, "damage"));
        GameThread.Run(() => { }, blocking: true);
        broker.Publish(first, new NetworkNavalLabFault(manifest.IncarnationId, "damage"));
        GameThread.Run(() => { }, blocking: true);
        var stop = Guid.NewGuid();
        coordinator.Execute(stop, "stop", 0, 0, false);
        coordinator.Execute(stop, "stop", 0, 0, false);
        foreach (var peer in new[] { first, second })
            Assert.Equal(new[] { "hold", "stop" }, network.GetPeerMessagesFromType<NetworkNavalLabAction>(peer).Select(value => value.Kind));
    }

    [Fact]
    public void ClientEmergencyActions_BypassExhaustedOrdinaryBudgetAndDeduplicate()
    {
        Start();
        for (int i = 1; i < 64; i++) store.BeginOperation(Guid.NewGuid(), "helm");
        foreach (var kind in new[] { "hold", "stop" })
        {
            var action = new NetworkNavalLabAction(manifest.IncarnationId, Guid.NewGuid(), 0, kind, 0, 0, false);
            broker.Publish(this, action);
            broker.Publish(this, action);
            GameThread.Run(() => { }, blocking: true);
            controller.Verify(value => value.Apply(action), Times.Once);
        }
    }

    public void Dispose()
    {
        coordinator.Dispose();
        broker.Dispose();
        ModInformation.IsServer = previousRole;
        typeof(ModInformation).GetProperty(nameof(ModInformation.NavalLabCapability))!.SetValue(null, previousCapability);
    }
}
#endif
