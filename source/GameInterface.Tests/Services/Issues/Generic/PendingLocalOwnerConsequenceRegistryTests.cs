using GameInterface.Services.Issues.Generic;
using GameInterface.Services.Players;
using GameInterface.Services.Players.Data;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace GameInterface.Tests.Services.Issues.Generic;

public class PendingLocalOwnerConsequenceRegistryTests
{
    private const string ControllerId = "PlayerOne";
    private const string QuestTypeKey = "SomeQuestType";
    private const byte Proof = 7;

    private static IPlayerManager CreateReadyPlayerManager(string controllerId)
    {
        var player = new Player(controllerId, string.Empty, string.Empty, string.Empty, string.Empty);
        var playerManager = new Mock<IPlayerManager>();
        playerManager.Setup(p => p.TryGetPlayer(controllerId, out player)).Returns(true);
        playerManager.Setup(p => p.IsCampaignReady(player)).Returns(true);
        return playerManager.Object;
    }

    [Fact]
    public void FlushReady_PeerLookupFailsDuringDelivery_LeavesTheObligationPendingForRetry()
    {
        var registry = new PendingLocalOwnerConsequenceRegistry();
        registry.DeferQuestFail(ControllerId, QuestTypeKey, Proof);
        var playerManager = CreateReadyPlayerManager(ControllerId);

        registry.FlushReady(playerManager, (controllerId, obligationId, questTypeKey, proof) => { });

        var pending = Assert.Single(registry.Snapshot());
        Assert.Equal(ControllerId, pending.ControllerId);
        Assert.Equal(QuestTypeKey, pending.QuestTypeKey);
        Assert.Equal(Proof, pending.Proof);
    }

    [Fact]
    public void FlushReady_DeliverThrows_LeavesTheObligationPendingForRetry()
    {
        var registry = new PendingLocalOwnerConsequenceRegistry();
        registry.DeferQuestFail(ControllerId, QuestTypeKey, Proof);
        var playerManager = CreateReadyPlayerManager(ControllerId);

        Assert.Throws<InvalidOperationException>(() =>
            registry.FlushReady(playerManager, (controllerId, obligationId, questTypeKey, proof) => throw new InvalidOperationException("send failed")));

        var pending = Assert.Single(registry.Snapshot());
        Assert.Equal(ControllerId, pending.ControllerId);
    }

    [Fact]
    public void Ack_RemovesOnlyTheAcknowledgedObligation()
    {
        var registry = new PendingLocalOwnerConsequenceRegistry();
        registry.DeferQuestFail(ControllerId, "QuestTypeA", 1);
        registry.DeferQuestFail(ControllerId, "QuestTypeB", 2);
        var firstObligationId = registry.Snapshot().First(e => e.QuestTypeKey == "QuestTypeA").ObligationId;

        registry.Ack(ControllerId, firstObligationId);

        var remaining = Assert.Single(registry.Snapshot());
        Assert.Equal("QuestTypeB", remaining.QuestTypeKey);
    }

    [Fact]
    public void Ack_UnknownObligationId_IsANoOp()
    {
        var registry = new PendingLocalOwnerConsequenceRegistry();
        registry.DeferQuestFail(ControllerId, QuestTypeKey, Proof);

        registry.Ack(ControllerId, 999);

        Assert.Single(registry.Snapshot());
    }

    [Fact]
    public void FlushReady_UnacknowledgedObligation_IsRedeliveredOnTheNextFlush()
    {
        var registry = new PendingLocalOwnerConsequenceRegistry();
        registry.DeferQuestFail(ControllerId, QuestTypeKey, Proof);
        var playerManager = CreateReadyPlayerManager(ControllerId);

        var deliveries = new List<long>();
        registry.FlushReady(playerManager, (controllerId, obligationId, questTypeKey, proof) => deliveries.Add(obligationId));
        registry.FlushReady(playerManager, (controllerId, obligationId, questTypeKey, proof) => deliveries.Add(obligationId));

        Assert.Equal(2, deliveries.Count);
        Assert.Equal(deliveries[0], deliveries[1]);
        Assert.Single(registry.Snapshot());
    }

    [Fact]
    public void FlushReady_AckedBetweenFlushes_IsNotRedelivered()
    {
        var registry = new PendingLocalOwnerConsequenceRegistry();
        registry.DeferQuestFail(ControllerId, QuestTypeKey, Proof);
        var playerManager = CreateReadyPlayerManager(ControllerId);

        var deliveries = new List<long>();
        registry.FlushReady(playerManager, (controllerId, obligationId, questTypeKey, proof) => deliveries.Add(obligationId));
        registry.Ack(ControllerId, deliveries[0]);
        registry.FlushReady(playerManager, (controllerId, obligationId, questTypeKey, proof) => deliveries.Add(obligationId));

        Assert.Single(deliveries);
        Assert.Empty(registry.Snapshot());
    }

    [Fact]
    public void Restore_KeepsTheSuppliedObligationIdAndAdvancesFutureIds()
    {
        var registry = new PendingLocalOwnerConsequenceRegistry();
        registry.Restore(ControllerId, 500, QuestTypeKey, Proof);

        var restored = Assert.Single(registry.Snapshot());
        Assert.Equal(500, restored.ObligationId);

        registry.DeferQuestFail(ControllerId, "AnotherQuestType", 1);
        var fresh = registry.Snapshot().Single(e => e.QuestTypeKey == "AnotherQuestType");
        Assert.True(fresh.ObligationId > 500);
    }
}
