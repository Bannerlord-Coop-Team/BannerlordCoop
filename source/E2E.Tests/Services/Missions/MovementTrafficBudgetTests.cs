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
