using Common.PacketHandlers;
using LiteNetLib;
using Missions;
using Missions.Agents.Handlers;
using Missions.Services.Network;
using Moq;
using System;
using System.Collections.Generic;
using Xunit;

namespace E2E.Tests.Services.Missions;

public sealed class MovementTrafficBudgetTests
{
    [Fact]
    public void TokenBucket_UsesActualBytesAndRefillsToBurstLimit()
    {
        var budget = new MovementTrafficBudget(bytesPerSecond: 100, burstBytes: 100);

        Assert.True(budget.TrySpend(80));
        Assert.False(budget.TrySpend(30));
        budget.Advance(0.5f);

        Assert.Equal(70, budget.AvailableBytes);
        Assert.True(budget.TrySpend(70));
        Assert.Equal(0, budget.AvailableBytes);
    }

    [Fact]
    public void TokenBucket_AccumulatesFractionalByteRates()
    {
        var budget = new MovementTrafficBudget(bytesPerSecond: 0.25d, burstBytes: 1);
        Assert.True(budget.TrySpend(1));

        budget.Advance(3f);
        Assert.Equal(0, budget.AvailableBytes);
        budget.Advance(1f);

        Assert.Equal(1, budget.AvailableBytes);
    }

    [Fact]
    public void TokenBucket_ReconfigurationDoesNotRefillTokens()
    {
        var budget = new MovementTrafficBudget(bytesPerSecond: 100, burstBytes: 100);
        Assert.True(budget.TrySpend(80));

        budget.Configure(bytesPerSecond: 100, burstBytes: 100);
        Assert.Equal(20, budget.AvailableBytes);

        budget.Configure(bytesPerSecond: 50, burstBytes: 10);
        Assert.Equal(10, budget.AvailableBytes);
        Assert.Equal(50, budget.BytesPerSecond);
        Assert.Equal(10, budget.BurstBytes);
    }

    [Fact]
    public void Sender_RotatesDeferredAgentsAndUsesTheirNewestState()
    {
        var network = new Mock<IBattleNetwork>();
        var compressor = new SizedPacketCompressor();
        var budget = new MovementTrafficBudget(bytesPerSecond: 30, burstBytes: 30);
        var sender = new MovementBatchSender(network.Object, compressor, () => budget);
        var sent = new List<(Guid Id, int Value)>();
        Guid[] ids = CreateIds(10);

        MovementSendResult first = sender.Send(
            "receiver",
            new[] { CreateBatch(ids, valueOffset: 0) },
            legacyBatch: null,
            maxPayloadBytes: 1000,
            CreatePacket,
            (id, value) => sent.Add((id, value)));
        Assert.Equal(3, first.SentCount);
        Assert.Equal(ids[0..3], sent.ConvertAll(item => item.Id));

        sender.BeginFrame(1f);
        MovementSendResult second = sender.Send(
            "receiver",
            new[] { CreateBatch(ids, valueOffset: 100) },
            legacyBatch: null,
            maxPayloadBytes: 1000,
            CreatePacket,
            (id, value) => sent.Add((id, value)));

        Assert.Equal(3, second.SentCount);
        Assert.Equal(ids[3..6], sent.GetRange(3, 3).ConvertAll(item => item.Id));
        Assert.Equal(new[] { 103, 104, 105 }, sent.GetRange(3, 3).ConvertAll(item => item.Value));
    }

    [Fact]
    public void Sender_UsesPriorityOrderWithinAnIdentityScope()
    {
        var network = new Mock<IBattleNetwork>();
        var compressor = new SizedPacketCompressor();
        var budget = new MovementTrafficBudget(bytesPerSecond: 10, burstBytes: 10);
        var sender = new MovementBatchSender(network.Object, compressor, () => budget);
        var scheduler = new MovementPriorityScheduler();
        Guid farId = Guid.NewGuid();
        Guid closeId = Guid.NewGuid();
        var batch = new MovementBatch<int>(null);
        batch.CanonicalIds.Add(farId);
        batch.Data.Add(1);
        batch.Priorities.Add(scheduler.CreateKey(
            false, 75f, 1f, 0.9f, 0.9f, farId));
        batch.CanonicalIds.Add(closeId);
        batch.Data.Add(2);
        batch.Priorities.Add(scheduler.CreateKey(
            false, 0f, 1f, 0.9f, 0.9f, closeId));
        var sent = new List<Guid>();

        MovementSendResult result = sender.Send(
            "receiver",
            new[] { batch },
            legacyBatch: null,
            maxPayloadBytes: 1000,
            CreatePacket,
            (id, value) => sent.Add(id));

        Assert.Equal(1, result.SentCount);
        Assert.Equal(closeId, Assert.Single(sent));
    }

    [Fact]
    public void Sender_CircularRotationWrapsInOrderAndUsesTheNewestState()
    {
        var network = new Mock<IBattleNetwork>();
        var compressor = new SizedPacketCompressor();
        var budget = new MovementTrafficBudget(bytesPerSecond: 20, burstBytes: 20);
        var sender = new MovementBatchSender(network.Object, compressor, () => budget);
        var sent = new List<(Guid Id, int Value)>();
        Guid[] ids = CreateIds(5);

        for (int cycle = 0; cycle < 3; cycle++)
        {
            if (cycle > 0) sender.BeginFrame(1f);
            sender.Send(
                "receiver",
                new[] { CreateBatch(ids, valueOffset: cycle * 100) },
                legacyBatch: null,
                maxPayloadBytes: 1000,
                CreatePacket,
                (id, value) => sent.Add((id, value)));
        }

        Assert.Equal(
            new[] { ids[0], ids[1], ids[2], ids[3], ids[4], ids[0] },
            sent.ConvertAll(item => item.Id));
        Assert.Equal(
            new[] { 0, 1, 102, 103, 204, 200 },
            sent.ConvertAll(item => item.Value));
    }

    [Fact]
    public void Sender_OneThousandMovingAgentsAllAdvanceUnderSustainedBudgetPressure()
    {
        var network = new Mock<IBattleNetwork>();
        var compressor = new SizedPacketCompressor();
        var budget = new MovementTrafficBudget(bytesPerSecond: 100, burstBytes: 100);
        var sender = new MovementBatchSender(network.Object, compressor, () => budget);
        Guid[] ids = CreateIds(1000);
        var sentIds = new HashSet<Guid>();

        for (int cycle = 0; cycle < 100; cycle++)
        {
            if (cycle > 0) sender.BeginFrame(1f);
            sender.Send(
                "receiver",
                new[] { CreateBatch(ids, valueOffset: cycle * 1000) },
                legacyBatch: null,
                maxPayloadBytes: 1000,
                CreatePacket,
                (id, value) => sentIds.Add(id));
        }

        Assert.Equal(ids.Length, sentIds.Count);
        Assert.All(ids, id => Assert.Contains(id, sentIds));
    }

    [Fact]
    public void Sender_RotatesAcrossIdentityScopesUnderSustainedBudgetPressure()
    {
        var network = new Mock<IBattleNetwork>();
        var compressor = new SizedPacketCompressor();
        var budget = new MovementTrafficBudget(bytesPerSecond: 10, burstBytes: 10);
        var sender = new MovementBatchSender(network.Object, compressor, () => budget);
        Guid firstId = Guid.NewGuid();
        Guid secondId = Guid.NewGuid();
        var sentIds = new List<Guid>();

        sender.Send(
            "receiver",
            new[]
            {
                CreateCompactBatch("first", firstId, 1),
                CreateCompactBatch("second", secondId, 2),
            },
            legacyBatch: null,
            maxPayloadBytes: 1000,
            CreatePacket,
            (id, value) => sentIds.Add(id));

        sender.BeginFrame(1f);
        sender.Send(
            "receiver",
            new[]
            {
                CreateCompactBatch("first", firstId, 3),
                CreateCompactBatch("second", secondId, 4),
            },
            legacyBatch: null,
            maxPayloadBytes: 1000,
            CreatePacket,
            (id, value) => sentIds.Add(id));

        Assert.Equal(new[] { firstId, secondId }, sentIds);
    }

    [Fact]
    public void Sender_InterleavesMovementTypesAfterEveryPacket()
    {
        var network = new Mock<IBattleNetwork>();
        var compressor = new SizedPacketCompressor();
        var budget = new MovementTrafficBudget(bytesPerSecond: 60, burstBytes: 60);
        var sender = new MovementBatchSender(network.Object, compressor, () => budget);
        var first = new MovementBatch<int>(null);
        var second = new MovementBatch<string>(null);
        var sent = new List<string>();

        for (int i = 0; i < 3; i++)
        {
            Guid firstId = Guid.NewGuid();
            first.CanonicalIds.Add(firstId);
            first.Data.Add(i);
            first.Priorities.Add(new MovementPriorityKey(
                1, (i * 2) + 1, 0f, 0f, firstId));

            Guid secondId = Guid.NewGuid();
            second.CanonicalIds.Add(secondId);
            second.Data.Add(i.ToString());
            second.Priorities.Add(new MovementPriorityKey(
                1, (i * 2) + 2, 0f, 0f, secondId));
        }

        MovementSendPairResult result = sender.SendInterleaved(
            "receiver",
            new[] { first },
            firstLegacyBatch: null,
            (scope, compactIds, canonicalIds, data) => new SizedPacket(data.Length),
            (id, value) => sent.Add($"first-{value}"),
            new[] { second },
            secondLegacyBatch: null,
            (scope, compactIds, canonicalIds, data) => new SizedPacket(data.Length),
            (id, value) => sent.Add($"second-{value}"),
            maxPayloadBytes: 20);

        Assert.Equal(3, result.First.SentCount);
        Assert.Equal(3, result.Second.SentCount);
        Assert.Equal(0, result.PrioritySentCount);
        Assert.Equal(6, result.BulkSentCount);
        Assert.Equal(
            new[] { "first-0", "second-0", "first-1", "second-1", "first-2", "second-2" },
            sent);
    }

    [Fact]
    public void Sender_ReservesSharedBalanceForBlockedPriorityPacket()
    {
        var network = new Mock<IBattleNetwork>();
        var compressor = new SizedPacketCompressor();
        var sharedBudget = new MovementTrafficBudget(bytesPerSecond: 100, burstBytes: 150);
        Assert.True(sharedBudget.TrySpend(50));
        var budgets = new Queue<IMovementTrafficBudget>(new[]
        {
            sharedBudget,
            new MovementTrafficBudget(bytesPerSecond: 1000, burstBytes: 1000),
        });
        var sender = new MovementBatchSender(
            network.Object,
            compressor,
            new QueueBudgetFactory(budgets),
            new MovementPriorityScheduler(),
            new MovementNetworkSettings(
                100d / MovementNetworkSettings.BytesPerMiB,
                1d));
        Guid priorityId = Guid.NewGuid();
        var priority = new MovementBatch<int>(null, isPriority: true);
        priority.CanonicalIds.Add(priorityId);
        priority.Data.Add(15);
        priority.Priorities.Add(new MovementPriorityKey(0, 0d, 0f, 0f, priorityId));
        Guid bulkId = Guid.NewGuid();
        var bulk = new MovementBatch<int>(null);
        bulk.CanonicalIds.Add(bulkId);
        bulk.Data.Add(10);
        bulk.Priorities.Add(new MovementPriorityKey(1, 1d, 0f, 0f, bulkId));
        var sent = new List<Guid>();

        MovementSendPairResult blocked = sender.SendInterleaved(
            "receiver",
            new[] { priority, bulk },
            firstLegacyBatch: null,
            (scope, compactIds, canonicalIds, data) => new SizedPacket(data[0]),
            (id, value) => sent.Add(id),
            Array.Empty<MovementBatch<string>>(),
            secondLegacyBatch: null,
            (scope, compactIds, canonicalIds, data) => new SizedPacket(data.Length),
            onSecondSent: null,
            maxPayloadBytes: 200);

        Assert.Empty(sent);
        Assert.True(blocked.BlockedBySharedBudget);
        Assert.True(blocked.PriorityBlockedBySharedBudget);
        Assert.Equal(150, blocked.RequiredSharedBudgetBytes);
        Assert.Equal(100, sharedBudget.AvailableBytes);

        sender.BeginFrame(0.5f);
        MovementSendPairResult sentAfterRefill = sender.SendInterleaved(
            "receiver",
            new[] { priority, bulk },
            firstLegacyBatch: null,
            (scope, compactIds, canonicalIds, data) => new SizedPacket(data[0]),
            (id, value) => sent.Add(id),
            Array.Empty<MovementBatch<string>>(),
            secondLegacyBatch: null,
            (scope, compactIds, canonicalIds, data) => new SizedPacket(data.Length),
            onSecondSent: null,
            maxPayloadBytes: 200);

        Assert.Equal(priorityId, Assert.Single(sent));
        Assert.Equal(1, sentAfterRefill.PrioritySentCount);
        Assert.Equal(0, sentAfterRefill.BulkSentCount);
    }

    [Fact]
    public void Sender_DoesNotReserveSharedBudgetWhenRecipientIsAlsoShort()
    {
        var network = new Mock<IBattleNetwork>();
        var sharedBudget = new MovementTrafficBudget(bytesPerSecond: 100, burstBytes: 150);
        Assert.True(sharedBudget.TrySpend(50));
        var budgets = new Queue<IMovementTrafficBudget>(new[]
        {
            sharedBudget,
            new MovementTrafficBudget(bytesPerSecond: 100, burstBytes: 100),
        });
        var sender = new MovementBatchSender(
            network.Object,
            new SizedPacketCompressor(),
            new QueueBudgetFactory(budgets),
            new MovementPriorityScheduler(),
            new MovementNetworkSettings(
                100d / MovementNetworkSettings.BytesPerMiB,
                1d));
        Guid agentId = Guid.NewGuid();
        var batch = new MovementBatch<int>(null);
        batch.CanonicalIds.Add(agentId);
        batch.Data.Add(15);
        batch.Priorities.Add(new MovementPriorityKey(1, 1d, 0f, 0f, agentId));

        MovementSendResult result = sender.Send(
            "receiver",
            new[] { batch },
            legacyBatch: null,
            maxPayloadBytes: 200,
            (scope, compactIds, canonicalIds, data) => new SizedPacket(data[0]),
            onSent: null);

        Assert.False(result.BlockedBySharedBudget);
        Assert.Equal(0, result.RequiredSharedBudgetBytes);
    }

    [Fact]
    public void Sender_SharedOutgoingBudgetCapsAllRecipientsTogether()
    {
        var network = new Mock<IBattleNetwork>();
        var compressor = new SizedPacketCompressor();
        var budgets = new Queue<IMovementTrafficBudget>(new[]
        {
            new MovementTrafficBudget(bytesPerSecond: 20, burstBytes: 20),
            new MovementTrafficBudget(bytesPerSecond: 100, burstBytes: 100),
            new MovementTrafficBudget(bytesPerSecond: 100, burstBytes: 100),
        });
        var sender = new MovementBatchSender(
            network.Object,
            compressor,
            new QueueBudgetFactory(budgets),
            new MovementPriorityScheduler(),
            new MovementNetworkSettings(
                20d / MovementNetworkSettings.BytesPerMiB,
                1d));
        Guid[] ids = CreateIds(3);

        MovementSendResult first = sender.Send(
            "first",
            new[] { CreateBatch(ids, valueOffset: 0) },
            legacyBatch: null,
            maxPayloadBytes: 1000,
            CreatePacket,
            onSent: null);
        MovementSendResult second = sender.Send(
            "second",
            new[] { CreateBatch(ids, valueOffset: 0) },
            legacyBatch: null,
            maxPayloadBytes: 1000,
            CreatePacket,
            onSent: null);

        Assert.Equal(2, first.SentCount);
        Assert.Equal(0, second.SentCount);
        Assert.Equal(0, sender.AvailableOutgoingBytes);

        sender.BeginFrame(1f);
        second = sender.Send(
            "second",
            new[] { CreateBatch(ids, valueOffset: 100) },
            legacyBatch: null,
            maxPayloadBytes: 1000,
            CreatePacket,
            onSent: null);

        Assert.Equal(2, second.SentCount);
    }

    [Fact]
    public void Sender_TracksBudgetAndFairnessPerRecipient()
    {
        var network = new Mock<IBattleNetwork>();
        var compressor = new SizedPacketCompressor();
        var budgets = new Queue<IMovementTrafficBudget>(new[]
        {
            new MovementTrafficBudget(bytesPerSecond: 10, burstBytes: 10),
            new MovementTrafficBudget(bytesPerSecond: 100, burstBytes: 100),
        });
        var sender = new MovementBatchSender(
            network.Object,
            compressor,
            () => budgets.Dequeue());
        Guid[] ids = CreateIds(10);

        MovementSendResult slow = sender.Send(
            "slow",
            new[] { CreateBatch(ids, valueOffset: 0) },
            legacyBatch: null,
            maxPayloadBytes: 1000,
            CreatePacket,
            onSent: null);
        MovementSendResult fast = sender.Send(
            "fast",
            new[] { CreateBatch(ids, valueOffset: 0) },
            legacyBatch: null,
            maxPayloadBytes: 1000,
            CreatePacket,
            onSent: null);

        Assert.Equal(1, slow.SentCount);
        Assert.Equal(9, slow.DeferredCount);
        Assert.Equal(10, fast.SentCount);
        Assert.Equal(0, fast.DeferredCount);
        network.Verify(
            value => value.Send("slow", It.IsAny<IPacket>(), It.IsAny<byte[]>()),
            Times.Once);
        network.Verify(
            value => value.Send("fast", It.IsAny<IPacket>(), It.IsAny<byte[]>()),
            Times.AtLeastOnce);
    }

    private static Guid[] CreateIds(int count)
    {
        var ids = new Guid[count];
        for (int i = 0; i < count; i++) ids[i] = Guid.NewGuid();
        return ids;
    }

    private static MovementBatch<int> CreateBatch(Guid[] ids, int valueOffset)
    {
        var batch = new MovementBatch<int>(null);
        for (int i = 0; i < ids.Length; i++)
        {
            batch.CanonicalIds.Add(ids[i]);
            batch.Data.Add(valueOffset + i);
        }
        return batch;
    }

    private static MovementBatch<int> CreateCompactBatch(
        string scopeId,
        Guid id,
        int value)
    {
        var batch = new MovementBatch<int>(scopeId);
        batch.CanonicalIds.Add(id);
        batch.CompactIds.Add(1);
        batch.Data.Add(value);
        return batch;
    }

    private static IPacket CreatePacket(
        string identityScopeId,
        ushort[] compactIds,
        Guid[] canonicalIds,
        int[] data) => new SizedPacket(data.Length);

    private sealed class QueueBudgetFactory : IMovementTrafficBudgetFactory
    {
        private readonly Queue<IMovementTrafficBudget> budgets;

        public QueueBudgetFactory(Queue<IMovementTrafficBudget> budgets)
        {
            this.budgets = budgets;
        }

        public IMovementTrafficBudget Create(double bytesPerSecond, int burstBytes) =>
            budgets.Dequeue();
    }

    private sealed class SizedPacketCompressor : IMovementPacketCompressor
    {
        public byte[] Serialize(IPacket packet) =>
            new byte[((SizedPacket)packet).Count * 10];

        public bool TryRestore(IPacket packet, out IPacket restored)
        {
            restored = packet;
            return true;
        }
    }

    private sealed class SizedPacket : IPacket
    {
        public PacketType PacketType => PacketType.Movement;
        public DeliveryMethod DeliveryMethod => DeliveryMethod.Unreliable;
        public int Count { get; }

        public SizedPacket(int count)
        {
            Count = count;
        }
    }
}
