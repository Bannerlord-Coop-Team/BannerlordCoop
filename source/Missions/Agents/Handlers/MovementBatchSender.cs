using Common.Logging;
using Common.PacketHandlers;
using GameInterface.Services.Entity;
using Missions.Services.Network;
using Serilog;
using System;
using System.Collections.Generic;

namespace Missions.Agents.Handlers;

public interface IMovementBatchSender
{
    void BeginFrame(float elapsedSeconds);

    MovementSendResult Send<T>(
        IEnumerable<MovementBatch<T>> scopedBatches,
        MovementBatch<T> legacyBatch,
        int maxPayloadBytes,
        Func<string, ushort[], Guid[], T[], IPacket> createPacket,
        Action<Guid, T> onSent);

    MovementTrafficFrame EndFrame(int deferredSnapshots, float maximumDeferredAgeSeconds);

    void Clear();
}

public readonly struct MovementSendResult
{
    public int SentCount { get; }
    public int DeferredCount { get; }

    public MovementSendResult(int sentCount, int deferredCount)
    {
        SentCount = sentCount;
        DeferredCount = deferredCount;
    }

    public MovementSendResult Add(MovementSendResult other) =>
        new MovementSendResult(
            SentCount + other.SentCount,
            DeferredCount + other.DeferredCount);
}

public sealed class MovementBatch<T>
{
    public string IdentityScopeId { get; }
    public bool IsPriority { get; }
    public List<ushort> CompactIds { get; } = new List<ushort>();
    public List<Guid> CanonicalIds { get; } = new List<Guid>();
    public List<T> Data { get; } = new List<T>();

    public MovementBatch(string identityScopeId, bool isPriority = false)
    {
        IdentityScopeId = identityScopeId;
        IsPriority = isPriority;
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
public sealed class MovementBatchSender : IMovementBatchSender
{
    private static readonly ILogger Logger = LogManager.GetLogger<MovementBatchSender>();
    private const int InitialBatchSize = 3;

    private readonly IBattleNetwork client;
    private readonly IMovementPacketCompressor packetCompressor;
    private readonly IMovementTrafficBudget trafficBudget;
    private readonly Dictionary<(Type SnapshotType, string IdentityScopeId, MovementIdFormat IdFormat), int>
        preferredBatchSizes =
            new Dictionary<(Type SnapshotType, string IdentityScopeId, MovementIdFormat IdFormat), int>();
    private readonly HashSet<(Type SnapshotType, string IdentityScopeId)> canonicalIdFallbacks =
        new HashSet<(Type SnapshotType, string IdentityScopeId)>();
    private readonly Dictionary<(Type SnapshotType, string IdentityScopeId, bool IsPriority), int> sendOffsets =
        new Dictionary<(Type SnapshotType, string IdentityScopeId, bool IsPriority), int>();
    private readonly Dictionary<(Type SnapshotType, bool IsPriority), int> batchOffsets =
        new Dictionary<(Type SnapshotType, bool IsPriority), int>();

    public MovementBatchSender(
        IBattleNetwork client,
        IMovementPacketCompressor packetCompressor,
        IMovementTrafficBudget trafficBudget = null)
    {
        this.client = client;
        this.packetCompressor = packetCompressor;
        this.trafficBudget = trafficBudget ?? new MovementTrafficBudget();
    }

    public void BeginFrame(float elapsedSeconds) => trafficBudget.Advance(elapsedSeconds);

    public MovementSendResult Send<T>(
        IEnumerable<MovementBatch<T>> scopedBatches,
        MovementBatch<T> legacyBatch,
        int maxPayloadBytes,
        Func<string, ushort[], Guid[], T[], IPacket> createPacket,
        Action<Guid, T> onSent)
    {
        var batches = new List<MovementBatch<T>>();
        foreach (MovementBatch<T> batch in scopedBatches)
        {
            if (batch != null && batch.Data.Count > 0)
                batches.Add(batch);
        }
        if (legacyBatch != null && legacyBatch.Data.Count > 0)
            batches.Add(legacyBatch);

        if (batches.Count == 0) return new MovementSendResult();

        var fairnessKey = (typeof(T), batches[0].IsPriority);
        int offset = batchOffsets.TryGetValue(fairnessKey, out int previousOffset)
            ? previousOffset % batches.Count
            : 0;
        var result = new MovementSendResult();
        for (int i = 0; i < batches.Count; i++)
        {
            MovementBatch<T> batch = batches[(offset + i) % batches.Count];
            result = result.Add(SendBatch(batch, maxPayloadBytes, createPacket, onSent));
        }

        batchOffsets[fairnessKey] = (offset + 1) % batches.Count;
        return result;
    }

    public MovementTrafficFrame EndFrame(int deferredSnapshots, float maximumDeferredAgeSeconds) =>
        trafficBudget.ReportFrame(deferredSnapshots, maximumDeferredAgeSeconds);

    public void Clear()
    {
        preferredBatchSizes.Clear();
        canonicalIdFallbacks.Clear();
        sendOffsets.Clear();
        batchOffsets.Clear();
        trafficBudget.Clear();
    }

    private MovementSendResult SendBatch<T>(
        MovementBatch<T> batch,
        int maxPayloadBytes,
        Func<string, ushort[], Guid[], T[], IPacket> createPacket,
        Action<Guid, T> onSent)
    {
        if (batch == null) return new MovementSendResult();
        if (batch.Data.Count == 0) return new MovementSendResult();
        if (maxPayloadBytes <= 0)
        {
            LogMissingPayloadBudget(batch);
            return new MovementSendResult(0, batch.Data.Count);
        }

        var fairnessKey = (typeof(T), batch.IdentityScopeId, batch.IsPriority);
        int offset = sendOffsets.TryGetValue(fairnessKey, out int previousOffset)
            ? previousOffset % batch.Data.Count
            : 0;
        MovementBatch<T> orderedBatch = Rotate(batch, offset);

        var fallbackKey = (typeof(T), batch.IdentityScopeId);
        MovementIdFormat idFormat =
            batch.IdentityScopeId == null || canonicalIdFallbacks.Contains(fallbackKey)
                ? MovementIdFormat.Canonical
                : MovementIdFormat.Compact;
        bool probeForGrowth = true;
        int sentCount = 0;

        for (int start = 0; start < orderedBatch.Data.Count;)
        {
            int availablePayloadBytes = Math.Min(maxPayloadBytes, trafficBudget.AvailableBytes);
            if (availablePayloadBytes <= 0) break;

            int remaining = orderedBatch.Data.Count - start;
            var preferenceKey = (typeof(T), batch.IdentityScopeId, idFormat);
            int preferredCount = preferredBatchSizes.TryGetValue(
                preferenceKey, out int previousSafeCount)
                ? previousSafeCount
                : InitialBatchSize;

            SerializedMovementBatch candidate = FindLargestFittingBatch(
                orderedBatch,
                start,
                preferredCount,
                probeForGrowth,
                availablePayloadBytes,
                idFormat,
                createPacket);

            if (idFormat == MovementIdFormat.Canonical &&
                !candidate.Fits(availablePayloadBytes) &&
                candidate.Fits(maxPayloadBytes))
            {
                break;
            }

            if (!candidate.Fits(availablePayloadBytes) && idFormat == MovementIdFormat.Compact)
            {
                SerializedMovementBatch canonicalCandidate = FindLargestFittingBatch(
                    orderedBatch,
                    start,
                    preferredCount,
                    probeForGrowth,
                    availablePayloadBytes,
                    MovementIdFormat.Canonical,
                    createPacket);
                if (!canonicalCandidate.Fits(availablePayloadBytes) &&
                    canonicalCandidate.Fits(maxPayloadBytes))
                {
                    break;
                }

                if (canonicalCandidate.Fits(availablePayloadBytes))
                {
                    idFormat = MovementIdFormat.Canonical;
                    canonicalIdFallbacks.Add(fallbackKey);
                    preferenceKey = (typeof(T), batch.IdentityScopeId, idFormat);
                    candidate = canonicalCandidate;
                }
                else
                {
                    LogOversizedCompactSnapshot(
                        orderedBatch.CanonicalIds[start],
                        candidate,
                        canonicalCandidate,
                        availablePayloadBytes);
                    start++;
                    continue;
                }
            }

            if (!candidate.Fits(availablePayloadBytes))
            {
                LogOversizedCanonicalSnapshot(
                    orderedBatch.CanonicalIds[start],
                    candidate,
                    availablePayloadBytes);
                start++;
                continue;
            }

            if (!trafficBudget.TrySpend(candidate.Payload.Length)) break;
            client.SendAll(candidate.Packet, candidate.Payload);
            for (int i = 0; i < candidate.Count; i++)
                onSent?.Invoke(
                    orderedBatch.CanonicalIds[start + i],
                    orderedBatch.Data[start + i]);

            if (availablePayloadBytes == maxPayloadBytes)
                RememberPreferredBatchSize(preferenceKey, candidate.Count, remaining);
            sentCount += candidate.Count;
            start += candidate.Count;
            probeForGrowth = false;
        }

        if (batch.Data.Count > 0)
            sendOffsets[fairnessKey] = (offset + sentCount) % batch.Data.Count;

        return new MovementSendResult(
            sentCount,
            Math.Max(0, batch.Data.Count - sentCount));
    }

    private static MovementBatch<T> Rotate<T>(MovementBatch<T> batch, int offset)
    {
        if (offset == 0) return batch;

        var rotated = new MovementBatch<T>(batch.IdentityScopeId, batch.IsPriority);
        for (int i = 0; i < batch.Data.Count; i++)
        {
            int source = (offset + i) % batch.Data.Count;
            rotated.CanonicalIds.Add(batch.CanonicalIds[source]);
            if (batch.IdentityScopeId != null)
                rotated.CompactIds.Add(batch.CompactIds[source]);
            rotated.Data.Add(batch.Data[source]);
        }
        return rotated;
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
