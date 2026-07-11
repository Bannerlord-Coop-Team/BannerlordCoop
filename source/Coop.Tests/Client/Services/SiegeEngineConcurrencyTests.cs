using Common.Tests.Utils;
using Coop.Core.Client.Services.SiegeEngines.Handlers;
using Coop.Core.Client.Services.SiegeEngines.Messages;
using Coop.Core.Server.Connections.Messages;
using Coop.Core.Server.Services.SiegeEngines.Handlers;
using Coop.Core.Server.Services.SiegeEngines.Messages;
using Coop.Tests.Mocks;
using GameInterface.Services.GameState.Messages;
using GameInterface.Services.ObjectManager;
using GameInterface.Services.SiegeEngines.Messages;
using GameInterface.Services.SiegeEvents.Interfaces;
using Moq;
using System.Linq;
using System.Runtime.Serialization;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using Xunit;
using static TaleWorlds.CampaignSystem.Siege.SiegeEvent;

namespace Coop.Tests.Client.Services;

public class SiegeEngineConcurrencyTests
{
    [Fact]
    public void DeployRequest_CarriesExpectedOccupantId()
    {
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var peer = network.CreatePeer();
        var objectManager = new Mock<IObjectManager>();
        var siegeEvent = CreateSiegeEvent();
        var container = new SiegeEnginesContainer(BattleSideEnum.Attacker, CreateEngine());
        var expectedOccupant = CreateEngine();
        var replacementType = new SiegeEngineType();
        string siegeEventId = "siege-1";
        string containerId = "container-1";
        string occupantId = "engine-a";
        objectManager.Setup(m => m.TryGetIdWithLogging<SiegeEvent>(siegeEvent, out siegeEventId)).Returns(true);
        objectManager.Setup(m => m.TryGetIdWithLogging<SiegeEnginesContainer>(container, out containerId)).Returns(true);
        objectManager.Setup(m => m.TryGetIdWithLogging<SiegeEngineConstructionProgress>(expectedOccupant, out occupantId)).Returns(true);
        var handler = new ClientSiegeEngineHandler(broker, network, objectManager.Object);
        broker.Publish(this, new NetworkSyncSiegeEngineSlotRevisions("epoch-a", new[]
        {
            new SiegeEngineSlotRevision(containerId, isRanged: false, index: 0, revision: 7L),
        }));

        broker.Publish(this, new SiegeEngineDeployRequested(
            siegeEvent,
            container,
            BattleSideEnum.Attacker,
            replacementType,
            0,
            expectedOccupant));

        var request = Assert.IsType<NetworkRequestDeploySiegeEngine>(Assert.Single(network.GetPeerMessages(peer)));
        Assert.Equal(occupantId, request.ExpectedOccupantId);
        Assert.Equal(7L, request.ExpectedRevision);
        Assert.Equal("epoch-a", request.RevisionEpoch);
        handler.Dispose();
    }

    [Fact]
    public void RemovalRequest_CarriesExpectedOccupantId()
    {
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var peer = network.CreatePeer();
        var objectManager = new Mock<IObjectManager>();
        var siegeEvent = CreateSiegeEvent();
        var container = new SiegeEnginesContainer(BattleSideEnum.Attacker, CreateEngine());
        var expectedOccupant = CreateEngine();
        string siegeEventId = "siege-1";
        string containerId = "container-1";
        string occupantId = "engine-a";
        objectManager.Setup(m => m.TryGetIdWithLogging<SiegeEvent>(siegeEvent, out siegeEventId)).Returns(true);
        objectManager.Setup(m => m.TryGetIdWithLogging<SiegeEnginesContainer>(container, out containerId)).Returns(true);
        objectManager.Setup(m => m.TryGetIdWithLogging<SiegeEngineConstructionProgress>(expectedOccupant, out occupantId)).Returns(true);
        var handler = new ClientSiegeEngineHandler(broker, network, objectManager.Object);
        broker.Publish(this, new NetworkSyncSiegeEngineSlotRevisions("epoch-a", new[]
        {
            new SiegeEngineSlotRevision(containerId, isRanged: false, index: 0, revision: 11L),
        }));
        broker.Publish(this, new NetworkChangeSiegeEngineUndeployed(
            containerId, index: 0, isRanged: false, moveToReserve: true, slotRevision: 10L, revisionEpoch: "epoch-a"));
        broker.Publish(this, new NetworkChangeSiegeEngineUndeployed(
            containerId, index: 0, isRanged: false, moveToReserve: true, slotRevision: 11L, revisionEpoch: "epoch-a"));
        broker.Publish(this, new NetworkChangeSiegeEngineUndeployed(
            containerId, index: 0, isRanged: false, moveToReserve: true, slotRevision: 12L, revisionEpoch: "old-epoch"));
        Assert.Empty(broker.GetMessagesFromType<ChangeSiegeEngineUndeployed>());

        broker.Publish(this, new SiegeEngineRemovalRequested(
            siegeEvent,
            container,
            BattleSideEnum.Attacker,
            0,
            isRanged: false,
            moveToReserve: true,
            expectedOccupant: expectedOccupant));

        var request = Assert.IsType<NetworkRequestRemoveSiegeEngine>(Assert.Single(network.GetPeerMessages(peer)));
        Assert.Equal(occupantId, request.ExpectedOccupantId);
        Assert.Equal(11L, request.ExpectedRevision);
        Assert.Equal("epoch-a", request.RevisionEpoch);
        handler.Dispose();
    }

    [Fact]
    public void SlotDelta_AppliesOnlyWhenEpochMatchesAndRevisionStrictlyAdvances()
    {
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var objectManager = new Mock<IObjectManager>();
        var handler = new ClientSiegeEngineHandler(broker, network, objectManager.Object);
        broker.Publish(this, new NetworkSyncSiegeEngineSlotRevisions("epoch-a", new[]
        {
            new SiegeEngineSlotRevision("container-1", isRanged: false, index: 0, revision: 2L),
        }));

        broker.Publish(this, new NetworkChangeSiegeEngineDeployed(
            "container-1", "engine-a", "type-a", index: 0, slotRevision: 3L, isRanged: false, revisionEpoch: "epoch-a"));
        broker.Publish(this, new NetworkChangeSiegeEngineDeployed(
            "container-1", "engine-b", "type-b", index: 0, slotRevision: 3L, isRanged: false, revisionEpoch: "epoch-a"));
        broker.Publish(this, new NetworkChangeSiegeEngineDeployed(
            "container-1", "engine-old", "type-old", index: 0, slotRevision: 2L, isRanged: false, revisionEpoch: "epoch-a"));
        broker.Publish(this, new NetworkChangeSiegeEngineDeployed(
            "container-1", "engine-other", "type-other", index: 0, slotRevision: 4L, isRanged: false, revisionEpoch: "old-epoch"));

        var applied = Assert.Single(broker.GetMessagesFromType<ChangeSiegeEngineDeployed>());
        Assert.Equal("engine-a", applied.SiegeEngineId);
        handler.Dispose();
    }

    [Fact]
    public void PreSnapshotDelta_ReplaysAfterAuthoritativeEpochArrives()
    {
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var objectManager = new Mock<IObjectManager>();
        var handler = new ClientSiegeEngineHandler(broker, network, objectManager.Object);

        broker.Publish(this, new NetworkChangeSiegeEngineDeployed(
            "container-1", "engine-a", "type-a", index: 0, slotRevision: 1L, isRanged: false, revisionEpoch: "epoch-a"));
        broker.Publish(this, new NetworkChangeSiegeEngineUndeployed(
            "container-1", index: 0, isRanged: false, moveToReserve: true, slotRevision: 2L, revisionEpoch: "epoch-a"));
        Assert.Empty(broker.GetMessagesFromType<ChangeSiegeEngineDeployed>());
        Assert.Empty(broker.GetMessagesFromType<ChangeSiegeEngineUndeployed>());

        broker.Publish(this, new NetworkSyncSiegeEngineSlotRevisions("epoch-a", new[]
        {
            new SiegeEngineSlotRevision("container-1", isRanged: false, index: 0, revision: 2L),
        }));

        var applied = broker.Messages
            .Where(message => message is ChangeSiegeEngineDeployed || message is ChangeSiegeEngineUndeployed)
            .ToArray();
        Assert.Equal(2, applied.Length);
        Assert.Equal("engine-a", Assert.IsType<ChangeSiegeEngineDeployed>(applied[0]).SiegeEngineId);
        Assert.IsType<ChangeSiegeEngineUndeployed>(applied[1]);
        handler.Dispose();
    }

    [Fact]
    public void PreSnapshotUiRequest_IsDroppedWhenReplayTouchedObservedSlotIncludingAba()
    {
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var peer = network.CreatePeer();
        var objectManager = new Mock<IObjectManager>();
        var siegeEvent = CreateSiegeEvent();
        var container = new SiegeEnginesContainer(BattleSideEnum.Attacker, CreateEngine());
        var oldOccupant = CreateEngine();
        string siegeEventId = "siege-1";
        string containerId = "container-1";
        string oldOccupantId = "engine-old";
        objectManager.Setup(m => m.TryGetIdWithLogging<SiegeEvent>(siegeEvent, out siegeEventId)).Returns(true);
        objectManager.Setup(m => m.TryGetIdWithLogging<SiegeEnginesContainer>(container, out containerId)).Returns(true);
        objectManager.Setup(m => m.TryGetIdWithLogging<SiegeEngineConstructionProgress>(oldOccupant, out oldOccupantId)).Returns(true);
        var handler = new ClientSiegeEngineHandler(broker, network, objectManager.Object);

        // The connection flushes post-save deltas ahead of its revision snapshot. The loaded UI can still
        // accept a click in that window. Even though replay returns the slot to the same occupant (ABA), the
        // click observed an older placement generation and must not be retargeted to the final A.
        broker.Publish(this, new NetworkChangeSiegeEngineDeployed(
            containerId, "engine-new", "type-a", index: 0, slotRevision: 1L, isRanged: false,
            revisionEpoch: "epoch-a"));
        broker.Publish(this, new NetworkChangeSiegeEngineDeployed(
            containerId, oldOccupantId, "type-a", index: 0, slotRevision: 2L, isRanged: false,
            revisionEpoch: "epoch-a"));
        broker.Publish(this, new SiegeEngineRemovalRequested(
            siegeEvent,
            container,
            BattleSideEnum.Attacker,
            0,
            isRanged: false,
            moveToReserve: true,
            expectedOccupant: oldOccupant));
        Assert.Empty(network.SentNetworkMessages);

        broker.Publish(this, new NetworkSyncSiegeEngineSlotRevisions("epoch-a", new[]
        {
            new SiegeEngineSlotRevision(containerId, isRanged: false, index: 0, revision: 2L),
        }));

        Assert.Empty(network.SentNetworkMessages);
        var applied = broker.GetMessagesFromType<ChangeSiegeEngineDeployed>().ToArray();
        Assert.Equal(2, applied.Length);
        Assert.Equal("engine-new", applied[0].SiegeEngineId);
        Assert.Equal(oldOccupantId, applied[1].SiegeEngineId);
        handler.Dispose();
    }

    [Fact]
    public void PreSnapshotUiRequest_SendsAfterSnapshotWhenObservedSlotWasUntouched()
    {
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var peer = network.CreatePeer();
        var objectManager = new Mock<IObjectManager>();
        var siegeEvent = CreateSiegeEvent();
        var container = new SiegeEnginesContainer(BattleSideEnum.Attacker, CreateEngine());
        var expectedOccupant = CreateEngine();
        string siegeEventId = "siege-1";
        string containerId = "container-1";
        string expectedOccupantId = "engine-a";
        objectManager.Setup(m => m.TryGetIdWithLogging<SiegeEvent>(siegeEvent, out siegeEventId)).Returns(true);
        objectManager.Setup(m => m.TryGetIdWithLogging<SiegeEnginesContainer>(container, out containerId)).Returns(true);
        objectManager.Setup(m => m.TryGetIdWithLogging<SiegeEngineConstructionProgress>(expectedOccupant, out expectedOccupantId)).Returns(true);
        var handler = new ClientSiegeEngineHandler(broker, network, objectManager.Object);

        broker.Publish(this, new SiegeEngineRemovalRequested(
            siegeEvent,
            container,
            BattleSideEnum.Attacker,
            0,
            isRanged: false,
            moveToReserve: true,
            expectedOccupant: expectedOccupant));
        Assert.Empty(network.SentNetworkMessages);

        broker.Publish(this, new NetworkSyncSiegeEngineSlotRevisions("epoch-a", new[]
        {
            new SiegeEngineSlotRevision(containerId, isRanged: false, index: 0, revision: 7L),
        }));

        var request = Assert.IsType<NetworkRequestRemoveSiegeEngine>(Assert.Single(network.GetPeerMessages(peer)));
        Assert.Equal(expectedOccupantId, request.ExpectedOccupantId);
        Assert.Equal(7L, request.ExpectedRevision);
        Assert.Equal("epoch-a", request.RevisionEpoch);
        handler.Dispose();
    }

    [Fact]
    public void NewEpochSnapshot_ReplacesHigherRevisionFromPriorCampaign()
    {
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var peer = network.CreatePeer();
        var objectManager = new Mock<IObjectManager>();
        var siegeEvent = CreateSiegeEvent();
        var container = new SiegeEnginesContainer(BattleSideEnum.Attacker, CreateEngine());
        var expectedOccupant = CreateEngine();
        string siegeEventId = "siege-1";
        string containerId = "container-1";
        string occupantId = "engine-a";
        objectManager.Setup(m => m.TryGetIdWithLogging<SiegeEvent>(siegeEvent, out siegeEventId)).Returns(true);
        objectManager.Setup(m => m.TryGetIdWithLogging<SiegeEnginesContainer>(container, out containerId)).Returns(true);
        objectManager.Setup(m => m.TryGetIdWithLogging<SiegeEngineConstructionProgress>(expectedOccupant, out occupantId)).Returns(true);
        var handler = new ClientSiegeEngineHandler(broker, network, objectManager.Object);
        broker.Publish(this, new NetworkSyncSiegeEngineSlotRevisions("old-epoch", new[]
        {
            new SiegeEngineSlotRevision(containerId, isRanged: false, index: 0, revision: 99L),
        }));
        broker.Publish(this, new CampaignReady());
        broker.Publish(this, new NetworkSyncSiegeEngineSlotRevisions("new-epoch", slots: null));

        broker.Publish(this, new SiegeEngineRemovalRequested(
            siegeEvent,
            container,
            BattleSideEnum.Attacker,
            0,
            isRanged: false,
            moveToReserve: true,
            expectedOccupant: expectedOccupant));

        var request = Assert.IsType<NetworkRequestRemoveSiegeEngine>(Assert.Single(network.GetPeerMessages(peer)));
        Assert.Equal(0L, request.ExpectedRevision);
        Assert.Equal("new-epoch", request.RevisionEpoch);
        handler.Dispose();
    }

    [Fact]
    public void SlotValidation_RejectsRequestAfterOccupantWasReplaced()
    {
        var container = new SiegeEnginesContainer(BattleSideEnum.Attacker, CreateEngine());
        var firstOccupant = CreateEngine();
        var replacement = CreateEngine();
        var objectManager = new Mock<IObjectManager>();
        string firstId = "engine-a";
        string replacementId = "engine-b";
        objectManager.Setup(m => m.TryGetId(firstOccupant, out firstId)).Returns(true);
        objectManager.Setup(m => m.TryGetId(replacement, out replacementId)).Returns(true);

        container.DeployedMeleeSiegeEngines[0] = firstOccupant;
        Assert.True(ServerSiegeEngineHandler.SlotMatchesExpectedState(
            container, 0, isRanged: false, expectedOccupantId: firstId, expectedRevision: 1L, expectedRevisionEpoch: "epoch-a",
            currentRevision: 1L, currentRevisionEpoch: "epoch-a", objectManager: objectManager.Object));
        Assert.False(ServerSiegeEngineHandler.SlotMatchesExpectedState(
            container, 0, isRanged: false, expectedOccupantId: firstId, expectedRevision: 1L, expectedRevisionEpoch: "old-epoch",
            currentRevision: 1L, currentRevisionEpoch: "epoch-a", objectManager: objectManager.Object));

        // A second honest UI action wins before the queued request drains. The old request must not mutate B.
        container.DeployedMeleeSiegeEngines[0] = replacement;
        Assert.False(ServerSiegeEngineHandler.SlotMatchesExpectedState(
            container, 0, isRanged: false, expectedOccupantId: firstId, expectedRevision: 1L, expectedRevisionEpoch: "epoch-a",
            currentRevision: 2L, currentRevisionEpoch: "epoch-a", objectManager: objectManager.Object));
        Assert.True(ServerSiegeEngineHandler.SlotMatchesExpectedState(
            container, 0, isRanged: false, expectedOccupantId: replacementId, expectedRevision: 2L, expectedRevisionEpoch: "epoch-a",
            currentRevision: 2L, currentRevisionEpoch: "epoch-a", objectManager: objectManager.Object));

        // ABA: the same object returns to the slot, but it is a later placement generation.
        container.DeployedMeleeSiegeEngines[0] = firstOccupant;
        Assert.False(ServerSiegeEngineHandler.SlotMatchesExpectedState(
            container, 0, isRanged: false, expectedOccupantId: firstId, expectedRevision: 1L, expectedRevisionEpoch: "epoch-a",
            currentRevision: 3L, currentRevisionEpoch: "epoch-a", objectManager: objectManager.Object));
        Assert.True(ServerSiegeEngineHandler.SlotMatchesExpectedState(
            container, 0, isRanged: false, expectedOccupantId: firstId, expectedRevision: 3L, expectedRevisionEpoch: "epoch-a",
            currentRevision: 3L, currentRevisionEpoch: "epoch-a", objectManager: objectManager.Object));
    }

    [Fact]
    public void SlotValidation_EmptyExpectationMatchesOnlyEmptySlot()
    {
        var container = new SiegeEnginesContainer(BattleSideEnum.Attacker, CreateEngine());
        var objectManager = new Mock<IObjectManager>();

        Assert.True(ServerSiegeEngineHandler.SlotMatchesExpectedState(
            container, 0, isRanged: false, expectedOccupantId: null, expectedRevision: 0L, expectedRevisionEpoch: "epoch-a",
            currentRevision: 0L, currentRevisionEpoch: "epoch-a", objectManager: objectManager.Object));

        container.DeployedMeleeSiegeEngines[0] = CreateEngine();
        Assert.False(ServerSiegeEngineHandler.SlotMatchesExpectedState(
            container, 0, isRanged: false, expectedOccupantId: null, expectedRevision: 0L, expectedRevisionEpoch: "epoch-a",
            currentRevision: 1L, currentRevisionEpoch: "epoch-a", objectManager: objectManager.Object));

        // Empty again is not the original empty generation.
        container.DeployedMeleeSiegeEngines[0] = null;
        Assert.False(ServerSiegeEngineHandler.SlotMatchesExpectedState(
            container, 0, isRanged: false, expectedOccupantId: null, expectedRevision: 0L, expectedRevisionEpoch: "epoch-a",
            currentRevision: 2L, currentRevisionEpoch: "epoch-a", objectManager: objectManager.Object));
    }

    [Fact]
    public void ServerMutations_AdvanceRevision_AndCatchUpNewClient()
    {
        var broker = new TestMessageBroker();
        var network = new TestNetwork();
        var peer = network.CreatePeer();
        var objectManager = new Mock<IObjectManager>();
        var siegeEventInterface = new Mock<ISiegeEventInterface>();
        var container = new SiegeEnginesContainer(BattleSideEnum.Attacker, CreateEngine());
        var engine = CreateEngine();
        string containerId = "container-1";
        string engineId = "engine-a";
        objectManager.Setup(m => m.TryGetIdWithLogging<SiegeEnginesContainer>(container, out containerId)).Returns(true);
        objectManager.Setup(m => m.TryGetIdWithLogging<SiegeEngineConstructionProgress>(engine, out engineId)).Returns(true);
        var handler = new ServerSiegeEngineHandler(broker, network, objectManager.Object, siegeEventInterface.Object);

        broker.Publish(this, new SiegeEngineDeployed(container, engine, index: 0));
        broker.Publish(this, new SiegeEngineUndeployed(container, index: 0, isRanged: false, moveToReserve: true));

        var deployed = Assert.Single(network.GetPeerMessages(peer).OfType<NetworkChangeSiegeEngineDeployed>());
        var undeployed = Assert.Single(network.GetPeerMessages(peer).OfType<NetworkChangeSiegeEngineUndeployed>());
        Assert.Equal(1L, deployed.SlotRevision);
        Assert.Equal(2L, undeployed.SlotRevision);
        Assert.False(string.IsNullOrEmpty(deployed.RevisionEpoch));
        Assert.Equal(deployed.RevisionEpoch, undeployed.RevisionEpoch);

        broker.Publish(this, new PlayerCampaignEntered(peer));

        var snapshot = Assert.Single(network.GetPeerMessages(peer).OfType<NetworkSyncSiegeEngineSlotRevisions>());
        Assert.Equal(deployed.RevisionEpoch, snapshot.RevisionEpoch);
        var slot = Assert.Single(snapshot.Slots);
        Assert.Equal(containerId, slot.ContainerId);
        Assert.False(slot.IsRanged);
        Assert.Equal(0, slot.Index);
        Assert.Equal(2L, slot.Revision);

        broker.Publish(this, new CampaignReady());
        broker.Publish(this, new SiegeEngineDeployed(container, engine, index: 0));

        var deploymentsAfterReload = network.GetPeerMessages(peer).OfType<NetworkChangeSiegeEngineDeployed>().ToArray();
        Assert.Equal(2, deploymentsAfterReload.Length);
        Assert.Equal(1L, deploymentsAfterReload[1].SlotRevision);
        Assert.NotEqual(deploymentsAfterReload[0].RevisionEpoch, deploymentsAfterReload[1].RevisionEpoch);
        handler.Dispose();
    }

    private static SiegeEngineConstructionProgress CreateEngine()
    {
        return new SiegeEngineConstructionProgress(new SiegeEngineType(), 1f, 100f);
    }

#pragma warning disable SYSLIB0050 // Identity-only test double; no native siege constructor is invoked.
    private static SiegeEvent CreateSiegeEvent() =>
        (SiegeEvent)FormatterServices.GetUninitializedObject(typeof(SiegeEvent));
#pragma warning restore SYSLIB0050
}
