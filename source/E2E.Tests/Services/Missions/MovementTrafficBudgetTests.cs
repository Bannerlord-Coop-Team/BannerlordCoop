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
        var sender = new MovementBatchSender(network.Object, compressor, budget);
        var sent = new List<(Guid Id, int Value)>();
        Guid[] ids = CreateIds(10);

        MovementSendResult first = sender.Send(
            new[] { CreateBatch(ids, valueOffset: 0) },
            legacyBatch: null,
            maxPayloadBytes: 1000,
            CreatePacket,
            (id, value) => sent.Add((id, value)));
        Assert.Equal(3, first.SentCount);
        Assert.Equal(ids[0..3], sent.ConvertAll(item => item.Id));

        sender.BeginFrame(1f);
        MovementSendResult second = sender.Send(
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
    public void Sender_OneThousandMovingAgentsAllAdvanceUnderSustainedBudgetPressure()
    {
        var network = new Mock<IBattleNetwork>();
        var compressor = new SizedPacketCompressor();
        var budget = new MovementTrafficBudget(bytesPerSecond: 100, burstBytes: 100);
        var sender = new MovementBatchSender(network.Object, compressor, budget);
        Guid[] ids = CreateIds(1000);
        var sentIds = new HashSet<Guid>();

        for (int cycle = 0; cycle < 100; cycle++)
        {
            if (cycle > 0) sender.BeginFrame(1f);
            sender.Send(
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
        var sender = new MovementBatchSender(network.Object, compressor, budget);
        Guid firstId = Guid.NewGuid();
        Guid secondId = Guid.NewGuid();
        var sentIds = new List<Guid>();

        sender.Send(
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
    public void Sender_ReusesLearnedTargetUntilGrowthCadenceExpires()
    {
        var network = new Mock<IBattleNetwork>();
        var sentCounts = new List<int>();
        network.Setup(x => x.SendAll(It.IsAny<IPacket>(), It.IsAny<byte[]>()))
            .Callback<IPacket, byte[]>((packet, _) =>
                sentCounts.Add(((SizedPacket)packet).Count));
        var compressor = new SizedPacketCompressor();
        var sender = new MovementBatchSender(
            network.Object,
            compressor,
            new ControllableTrafficBudget(10000));
        Guid[] ids = CreateIds(12);

        sender.Send(
            new[] { CreateBatch(ids, valueOffset: 0) },
            legacyBatch: null,
            maxPayloadBytes: 50,
            CreatePacket,
            onSent: null);

        compressor.BytesPerSnapshot = 5;
        compressor.Reset();
        sentCounts.Clear();
        sender.BeginFrame(0.5f);
        sender.Send(
            new[] { CreateBatch(ids, valueOffset: 100) },
            legacyBatch: null,
            maxPayloadBytes: 50,
            CreatePacket,
            onSent: null);

        Assert.Equal(new[] { 5, 5, 2 }, sentCounts);
        Assert.Equal(sentCounts.Count, compressor.SerializeCalls);

        compressor.Reset();
        sentCounts.Clear();
        sender.BeginFrame(0.5f);
        sender.Send(
            new[] { CreateBatch(ids, valueOffset: 200) },
            legacyBatch: null,
            maxPayloadBytes: 50,
            CreatePacket,
            onSent: null);

        Assert.Equal(new[] { 10, 2 }, sentCounts);
        Assert.True(compressor.SerializeCalls > sentCounts.Count);
    }

    [Fact]
    public void Sender_ShrinksLearnedTargetImmediatelyWhenItNoLongerFits()
    {
        var network = new Mock<IBattleNetwork>();
        var sentCounts = new List<int>();
        network.Setup(x => x.SendAll(It.IsAny<IPacket>(), It.IsAny<byte[]>()))
            .Callback<IPacket, byte[]>((packet, _) =>
                sentCounts.Add(((SizedPacket)packet).Count));
        var compressor = new SizedPacketCompressor();
        var sender = new MovementBatchSender(
            network.Object,
            compressor,
            new ControllableTrafficBudget(10000));
        Guid[] ids = CreateIds(12);

        sender.Send(
            new[] { CreateBatch(ids, valueOffset: 0) },
            legacyBatch: null,
            maxPayloadBytes: 50,
            CreatePacket,
            onSent: null);

        compressor.BytesPerSnapshot = 20;
        compressor.Reset();
        sentCounts.Clear();
        sender.Send(
            new[] { CreateBatch(ids, valueOffset: 100) },
            legacyBatch: null,
            maxPayloadBytes: 50,
            CreatePacket,
            onSent: null);

        Assert.Equal(new[] { 2, 2, 2, 2, 2, 2 }, sentCounts);
        Assert.True(compressor.SerializeCalls > sentCounts.Count);
    }

    [Fact]
    public void Sender_ConstrainedBudgetDoesNotPostponeDueGrowthProbe()
    {
        var network = new Mock<IBattleNetwork>();
        var sentCounts = new List<int>();
        network.Setup(x => x.SendAll(It.IsAny<IPacket>(), It.IsAny<byte[]>()))
            .Callback<IPacket, byte[]>((packet, _) =>
                sentCounts.Add(((SizedPacket)packet).Count));
        var compressor = new SizedPacketCompressor();
        var budget = new ControllableTrafficBudget(50);
        var sender = new MovementBatchSender(network.Object, compressor, budget);
        Guid[] ids = CreateIds(12);

        sender.Send(
            new[] { CreateBatch(ids, valueOffset: 0) },
            legacyBatch: null,
            maxPayloadBytes: 50,
            CreatePacket,
            onSent: null);

        compressor.BytesPerSnapshot = 5;
        budget.AvailableBytes = 30;
        sentCounts.Clear();
        sender.BeginFrame(1f);
        sender.Send(
            new[] { CreateBatch(ids, valueOffset: 100) },
            legacyBatch: null,
            maxPayloadBytes: 50,
            CreatePacket,
            onSent: null);
        Assert.Equal(new[] { 5, 1 }, sentCounts);

        budget.AvailableBytes = 50;
        sentCounts.Clear();
        sender.BeginFrame(0.1f);
        sender.Send(
            new[] { CreateBatch(ids, valueOffset: 200) },
            legacyBatch: null,
            maxPayloadBytes: 50,
            CreatePacket,
            onSent: null);

        Assert.Equal(new[] { 10 }, sentCounts);
    }

    [Fact]
    public void Sender_GrowthCadenceIsIsolatedByScopeAndIdFormat()
    {
        var network = new Mock<IBattleNetwork>();
        var sentCounts = new List<int>();
        network.Setup(x => x.SendAll(It.IsAny<IPacket>(), It.IsAny<byte[]>()))
            .Callback<IPacket, byte[]>((packet, _) =>
                sentCounts.Add(((SizedPacket)packet).Count));
        var compressor = new SizedPacketCompressor();
        var sender = new MovementBatchSender(
            network.Object,
            compressor,
            new ControllableTrafficBudget(10000));
        Guid[] ids = CreateIds(12);

        sender.Send(
            new[] { CreateBatch(ids, valueOffset: 0) },
            legacyBatch: null,
            maxPayloadBytes: 50,
            CreatePacket,
            onSent: null);
        sender.Send(
            new[] { CreateCompactBatch("scope", ids, valueOffset: 0) },
            legacyBatch: null,
            maxPayloadBytes: 50,
            CreatePacket,
            onSent: null);

        compressor.BytesPerSnapshot = 5;
        sender.BeginFrame(1f);
        sentCounts.Clear();
        sender.Send(
            new[] { CreateBatch(ids, valueOffset: 100) },
            legacyBatch: null,
            maxPayloadBytes: 50,
            CreatePacket,
            onSent: null);
        Assert.Equal(10, sentCounts[0]);

        sentCounts.Clear();
        sender.Send(
            new[] { CreateCompactBatch("scope", ids, valueOffset: 100) },
            legacyBatch: null,
            maxPayloadBytes: 50,
            CreatePacket,
            onSent: null);

        Assert.Equal(10, sentCounts[0]);
    }

    [Fact]
    public void Sender_PriorityBatchDoesNotConsumeNormalGrowthCadence()
    {
        var network = new Mock<IBattleNetwork>();
        var sentCounts = new List<int>();
        network.Setup(x => x.SendAll(It.IsAny<IPacket>(), It.IsAny<byte[]>()))
            .Callback<IPacket, byte[]>((packet, _) =>
                sentCounts.Add(((SizedPacket)packet).Count));
        var compressor = new SizedPacketCompressor();
        var sender = new MovementBatchSender(
            network.Object,
            compressor,
            new ControllableTrafficBudget(10000));
        Guid[] ids = CreateIds(12);

        sender.Send(
            new[] { CreateCompactBatch("scope", ids[0..1], valueOffset: 0, isPriority: true) },
            legacyBatch: null,
            maxPayloadBytes: 50,
            CreatePacket,
            onSent: null);
        sentCounts.Clear();
        sender.Send(
            new[] { CreateCompactBatch("scope", ids, valueOffset: 0) },
            legacyBatch: null,
            maxPayloadBytes: 50,
            CreatePacket,
            onSent: null);
        Assert.Equal(5, sentCounts[0]);

        compressor.BytesPerSnapshot = 5;
        sender.BeginFrame(1f);
        sender.Send(
            new[] { CreateCompactBatch("scope", ids[0..1], valueOffset: 100, isPriority: true) },
            legacyBatch: null,
            maxPayloadBytes: 50,
            CreatePacket,
            onSent: null);
        sentCounts.Clear();
        sender.Send(
            new[] { CreateCompactBatch("scope", ids, valueOffset: 100) },
            legacyBatch: null,
            maxPayloadBytes: 50,
            CreatePacket,
            onSent: null);

        Assert.Equal(10, sentCounts[0]);
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

    private static MovementBatch<int> CreateCompactBatch(
        string scopeId,
        Guid[] ids,
        int valueOffset,
        bool isPriority = false)
    {
        var batch = new MovementBatch<int>(scopeId, isPriority);
        for (int i = 0; i < ids.Length; i++)
        {
            batch.CanonicalIds.Add(ids[i]);
            batch.CompactIds.Add((ushort)(i + 1));
            batch.Data.Add(valueOffset + i);
        }
        return batch;
    }

    private static IPacket CreatePacket(
        string identityScopeId,
        ushort[] compactIds,
        Guid[] canonicalIds,
        int[] data) => new SizedPacket(data.Length);

    private sealed class SizedPacketCompressor : IMovementPacketCompressor
    {
        public int BytesPerSnapshot { get; set; } = 10;
        public int SerializeCalls { get; private set; }

        public byte[] Serialize(IPacket packet) =>
            new byte[RecordSerialization((SizedPacket)packet)];

        public bool TryRestore(IPacket packet, out IPacket restored)
        {
            restored = packet;
            return true;
        }

        public void Reset()
        {
            SerializeCalls = 0;
        }

        private int RecordSerialization(SizedPacket packet)
        {
            SerializeCalls++;
            return packet.Count * BytesPerSnapshot;
        }
    }

    private sealed class ControllableTrafficBudget : IMovementTrafficBudget
    {
        private readonly int initialBytes;

        public int AvailableBytes { get; set; }

        public ControllableTrafficBudget(int availableBytes)
        {
            initialBytes = availableBytes;
            AvailableBytes = availableBytes;
        }

        public void Advance(float elapsedSeconds)
        {
        }

        public bool TrySpend(int bytes)
        {
            if (bytes <= 0 || bytes > AvailableBytes) return false;

            AvailableBytes -= bytes;
            return true;
        }

        public void ReportFrame(int deferredSnapshots, float maximumDeferredAgeSeconds)
        {
        }

        public void Clear()
        {
            AvailableBytes = initialBytes;
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
