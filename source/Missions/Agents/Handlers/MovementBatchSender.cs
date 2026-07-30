using Common.Logging;
using Common.PacketHandlers;
using GameInterface.Services.Entity;
using Missions.Services.Network;
using Serilog;
using System;
using System.Collections.Generic;

namespace Missions.Agents.Handlers;

internal sealed class MovementBatch<T>
{
    public string IdentityScopeId { get; }
    public List<ushort> CompactIds { get; } = new List<ushort>();
    public List<Guid> CanonicalIds { get; } = new List<Guid>();
    public List<T> Data { get; } = new List<T>();

    public MovementBatch(string identityScopeId)
    {
        IdentityScopeId = identityScopeId;
    }

    public void Add(CoopAgentInfo info, T data)
    {
        CanonicalIds.Add(info.AgentId);
        if (IdentityScopeId != null)
            CompactIds.Add(info.MovementId);
        Data.Add(data);
    }
}

/// <summary>Selects and sends the largest movement batches that fit the unreliable route budget.</summary>
internal sealed class MovementBatchSender
{
    private static readonly ILogger Logger = LogManager.GetLogger<MovementBatchSender>();
    private const int InitialBatchSize = 3;

    private readonly IBattleNetwork client;
    private readonly IMovementPacketCompressor packetCompressor;
    private readonly Dictionary<(Type SnapshotType, string IdentityScopeId, MovementIdFormat IdFormat), int>
        preferredBatchSizes =
            new Dictionary<(Type SnapshotType, string IdentityScopeId, MovementIdFormat IdFormat), int>();
    private readonly HashSet<(Type SnapshotType, string IdentityScopeId)> canonicalIdFallbacks =
        new HashSet<(Type SnapshotType, string IdentityScopeId)>();

    public MovementBatchSender(
        IBattleNetwork client,
        IMovementPacketCompressor packetCompressor)
    {
        this.client = client;
        this.packetCompressor = packetCompressor;
    }

    public void Send<T>(
        IEnumerable<MovementBatch<T>> scopedBatches,
        MovementBatch<T> legacyBatch,
        int maxPayloadBytes,
        Func<string, ushort[], Guid[], T[], IPacket> createPacket)
    {
        foreach (MovementBatch<T> batch in scopedBatches)
            SendBatch(batch, maxPayloadBytes, createPacket);

        SendBatch(legacyBatch, maxPayloadBytes, createPacket);
    }

    public void Clear()
    {
        preferredBatchSizes.Clear();
        canonicalIdFallbacks.Clear();
    }

    private void SendBatch<T>(
        MovementBatch<T> batch,
        int maxPayloadBytes,
        Func<string, ushort[], Guid[], T[], IPacket> createPacket)
    {
        if (batch == null) return;
        if (maxPayloadBytes <= 0)
        {
            LogMissingPayloadBudget(batch);
            return;
        }

        var fallbackKey = (typeof(T), batch.IdentityScopeId);
        MovementIdFormat idFormat =
            batch.IdentityScopeId == null || canonicalIdFallbacks.Contains(fallbackKey)
                ? MovementIdFormat.Canonical
                : MovementIdFormat.Compact;
        bool probeForGrowth = true;

        for (int start = 0; start < batch.Data.Count;)
        {
            int remaining = batch.Data.Count - start;
            var preferenceKey = (typeof(T), batch.IdentityScopeId, idFormat);
            int preferredCount = preferredBatchSizes.TryGetValue(
                preferenceKey, out int previousSafeCount)
                ? previousSafeCount
                : InitialBatchSize;

            SerializedMovementBatch candidate = FindLargestFittingBatch(
                batch,
                start,
                preferredCount,
                probeForGrowth,
                maxPayloadBytes,
                idFormat,
                createPacket);

            if (!candidate.Fits(maxPayloadBytes) && idFormat == MovementIdFormat.Compact)
            {
                SerializedMovementBatch canonicalCandidate = FindLargestFittingBatch(
                    batch,
                    start,
                    preferredCount,
                    probeForGrowth,
                    maxPayloadBytes,
                    MovementIdFormat.Canonical,
                    createPacket);
                if (canonicalCandidate.Fits(maxPayloadBytes))
                {
                    idFormat = MovementIdFormat.Canonical;
                    canonicalIdFallbacks.Add(fallbackKey);
                    preferenceKey = (typeof(T), batch.IdentityScopeId, idFormat);
                    candidate = canonicalCandidate;
                }
                else
                {
                    LogOversizedCompactSnapshot(
                        batch.CanonicalIds[start],
                        candidate,
                        canonicalCandidate,
                        maxPayloadBytes);
                    start++;
                    continue;
                }
            }

            if (!candidate.Fits(maxPayloadBytes))
            {
                LogOversizedCanonicalSnapshot(
                    batch.CanonicalIds[start],
                    candidate,
                    maxPayloadBytes);
                start++;
                continue;
            }

            client.SendAll(candidate.Packet, candidate.Payload);
            RememberPreferredBatchSize(preferenceKey, candidate.Count, remaining);
            start += candidate.Count;
            probeForGrowth = false;
        }
    }

    private static void LogMissingPayloadBudget<T>(MovementBatch<T> batch)
    {
        Logger.Warning(
            "[BattleTraffic] Skipping {SnapshotCount} {SnapshotType} snapshots for scope {IdentityScopeId}: " +
            "route framing leaves no unreliable payload budget",
            batch.Data.Count,
            typeof(T).Name,
            batch.IdentityScopeId ?? "legacy-guid");
    }

    private static void LogOversizedCompactSnapshot(
        Guid agentId,
        SerializedMovementBatch compactCandidate,
        SerializedMovementBatch canonicalCandidate,
        int maxPayloadBytes)
    {
        Logger.Warning(
            "[BattleTraffic] Skipping oversized {PacketType} snapshot {AgentId}: compact={CompactBytes}, " +
            "guid={GuidBytes}, budget={BudgetBytes}",
            compactCandidate.Packet.PacketType,
            agentId,
            compactCandidate.Payload.Length,
            canonicalCandidate.Payload.Length,
            maxPayloadBytes);
    }

    private static void LogOversizedCanonicalSnapshot(
        Guid agentId,
        SerializedMovementBatch candidate,
        int maxPayloadBytes)
    {
        Logger.Warning(
            "[BattleTraffic] Skipping oversized {PacketType} snapshot {AgentId}: payload={PayloadBytes}, " +
            "budget={BudgetBytes}",
            candidate.Packet.PacketType,
            agentId,
            candidate.Payload.Length,
            maxPayloadBytes);
    }

    private void RememberPreferredBatchSize(
        (Type SnapshotType, string IdentityScopeId, MovementIdFormat IdFormat) preferenceKey,
        int safeCount,
        int remaining)
    {
        if (safeCount < remaining ||
            !preferredBatchSizes.TryGetValue(preferenceKey, out int previousSafeCount) ||
            safeCount > previousSafeCount)
        {
            preferredBatchSizes[preferenceKey] = safeCount;
        }
    }

    private SerializedMovementBatch FindLargestFittingBatch<T>(
        MovementBatch<T> batch,
        int start,
        int preferredCount,
        bool probeForGrowth,
        int maxPayloadBytes,
        MovementIdFormat idFormat,
        Func<string, ushort[], Guid[], T[], IPacket> createPacket)
    {
        int remaining = batch.Data.Count - start;

        SerializedMovementBatch CreateCandidate(int count) =>
            CreateSerializedCandidate(batch, start, count, idFormat, createPacket);

        int initialCount = Math.Min(Math.Max(1, preferredCount), remaining);
        SerializedMovementBatch initialCandidate = CreateCandidate(initialCount);
        if (initialCandidate.Fits(maxPayloadBytes))
        {
            if (initialCount == remaining || !probeForGrowth)
                return initialCandidate;

            return GrowToPayloadLimit(
                remaining,
                initialCandidate,
                maxPayloadBytes,
                CreateCandidate);
        }

        return ShrinkToPayloadLimit(
            initialCandidate,
            maxPayloadBytes,
            CreateCandidate);
    }

    private static SerializedMovementBatch GrowToPayloadLimit(
        int remaining,
        SerializedMovementBatch initialCandidate,
        int maxPayloadBytes,
        Func<int, SerializedMovementBatch> createCandidate)
    {
        SerializedMovementBatch safeCandidate = initialCandidate;

        for (long offset = 1; ;)
        {
            int probeCount = (int)Math.Min(
                remaining,
                (long)initialCandidate.Count + offset);
            SerializedMovementBatch probe = createCandidate(probeCount);
            if (!probe.Fits(maxPayloadBytes))
            {
                return RefinePayloadLimit(
                    safeCandidate,
                    probeCount,
                    maxPayloadBytes,
                    createCandidate);
            }

            safeCandidate = probe;
            if (probeCount == remaining)
                return safeCandidate;

            offset *= 2;
        }
    }

    private static SerializedMovementBatch ShrinkToPayloadLimit(
        SerializedMovementBatch initialCandidate,
        int maxPayloadBytes,
        Func<int, SerializedMovementBatch> createCandidate)
    {
        if (initialCandidate.Count == 1)
            return initialCandidate;

        int smallestOversizedCount = initialCandidate.Count;
        for (long offset = 1; ;)
        {
            int probeCount = (int)Math.Max(
                1,
                (long)initialCandidate.Count - offset);
            SerializedMovementBatch probe = createCandidate(probeCount);
            if (probe.Fits(maxPayloadBytes))
            {
                return RefinePayloadLimit(
                    probe,
                    smallestOversizedCount,
                    maxPayloadBytes,
                    createCandidate);
            }

            smallestOversizedCount = probeCount;
            if (probeCount == 1)
                return probe;

            offset *= 2;
        }
    }

    private static SerializedMovementBatch RefinePayloadLimit(
        SerializedMovementBatch knownSafeCandidate,
        int knownOversizedCount,
        int maxPayloadBytes,
        Func<int, SerializedMovementBatch> createCandidate)
    {
        int low = knownSafeCandidate.Count;
        int high = knownOversizedCount;
        SerializedMovementBatch largestSafeCandidate = knownSafeCandidate;

        while (low + 1 < high)
        {
            int probeCount = low + ((high - low) / 2);
            SerializedMovementBatch probe = createCandidate(probeCount);
            if (probe.Fits(maxPayloadBytes))
            {
                low = probeCount;
                largestSafeCandidate = probe;
            }
            else
            {
                high = probeCount;
            }
        }

        return largestSafeCandidate;
    }

    private SerializedMovementBatch CreateSerializedCandidate<T>(
        MovementBatch<T> batch,
        int start,
        int count,
        MovementIdFormat idFormat,
        Func<string, ushort[], Guid[], T[], IPacket> createPacket)
    {
        var data = new T[count];
        batch.Data.CopyTo(start, data, 0, count);

        IPacket packet;
        if (idFormat == MovementIdFormat.Canonical)
        {
            var ids = new Guid[count];
            batch.CanonicalIds.CopyTo(start, ids, 0, count);
            packet = createPacket(null, null, ids, data);
        }
        else
        {
            var ids = new ushort[count];
            batch.CompactIds.CopyTo(start, ids, 0, count);
            packet = createPacket(batch.IdentityScopeId, ids, null, data);
        }

        return new SerializedMovementBatch(
            count,
            packet,
            packetCompressor.Serialize(packet));
    }

    private enum MovementIdFormat
    {
        Compact,
        Canonical,
    }

    private sealed class SerializedMovementBatch
    {
        public int Count { get; }
        public IPacket Packet { get; }
        public byte[] Payload { get; }

        public SerializedMovementBatch(int count, IPacket packet, byte[] payload)
        {
            Count = count;
            Packet = packet;
            Payload = payload;
        }

        public bool Fits(int maxPayloadBytes)
        {
            return Payload.Length <= maxPayloadBytes;
        }
    }
}
