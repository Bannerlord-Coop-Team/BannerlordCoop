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
        string controllerId,
        IEnumerable<MovementBatch<T>> scopedBatches,
        MovementBatch<T> legacyBatch,
        int maxPayloadBytes,
        Func<string, ushort[], Guid[], T[], IPacket> createPacket,
        Action<Guid, T> onSent);

    MovementTrafficFrame EndFrame(
        string controllerId,
        int deferredSnapshots,
        float maximumDeferredAgeSeconds);

    void RemoveRecipient(string controllerId);

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

/// <summary>Selects and sends the largest movement batches that fit each recipient's route budget.</summary>
public sealed class MovementBatchSender : IMovementBatchSender
{
    private static readonly ILogger Logger = LogManager.GetLogger<MovementBatchSender>();
    private const int InitialBatchSize = 3;

    private readonly IBattleNetwork client;
    private readonly IMovementPacketCompressor packetCompressor;
    private readonly Func<IMovementTrafficBudget> trafficBudgetFactory;
    private readonly Dictionary<string, RecipientState> recipients =
        new Dictionary<string, RecipientState>(StringComparer.Ordinal);

    private sealed class RecipientState
    {
        public IMovementTrafficBudget TrafficBudget { get; }
        public Dictionary<(Type SnapshotType, string IdentityScopeId, MovementIdFormat IdFormat), int>
            PreferredBatchSizes { get; } =
                new Dictionary<(Type, string, MovementIdFormat), int>();
        public HashSet<(Type SnapshotType, string IdentityScopeId)> CanonicalIdFallbacks { get; } =
            new HashSet<(Type, string)>();
        public Dictionary<(Type SnapshotType, string IdentityScopeId, bool IsPriority), int>
            SendOffsets { get; } =
                new Dictionary<(Type, string, bool), int>();
        public Dictionary<(Type SnapshotType, bool IsPriority), int> BatchOffsets { get; } =
            new Dictionary<(Type, bool), int>();

        public RecipientState(IMovementTrafficBudget trafficBudget)
        {
            TrafficBudget = trafficBudget;
        }
    }

    public MovementBatchSender(
        IBattleNetwork client,
        IMovementPacketCompressor packetCompressor,
        Func<IMovementTrafficBudget> trafficBudgetFactory)
    {
        if (client == null) throw new ArgumentNullException(nameof(client));
        if (packetCompressor == null) throw new ArgumentNullException(nameof(packetCompressor));
        if (trafficBudgetFactory == null) throw new ArgumentNullException(nameof(trafficBudgetFactory));

        this.client = client;
        this.packetCompressor = packetCompressor;
        this.trafficBudgetFactory = trafficBudgetFactory;
    }

    public void BeginFrame(float elapsedSeconds)
    {
        foreach (RecipientState recipient in recipients.Values)
            recipient.TrafficBudget.Advance(elapsedSeconds);
    }

    public MovementSendResult Send<T>(
        string controllerId,
        IEnumerable<MovementBatch<T>> scopedBatches,
        MovementBatch<T> legacyBatch,
        int maxPayloadBytes,
        Func<string, ushort[], Guid[], T[], IPacket> createPacket,
        Action<Guid, T> onSent)
    {
        RecipientState recipient = GetOrCreateRecipient(controllerId);
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
        int offset = recipient.BatchOffsets.TryGetValue(fairnessKey, out int previousOffset)
            ? previousOffset % batches.Count
            : 0;
        var result = new MovementSendResult();
        for (int i = 0; i < batches.Count; i++)
        {
            MovementBatch<T> batch = batches[(offset + i) % batches.Count];
            result = result.Add(SendBatch(
                controllerId,
                recipient,
                batch,
                maxPayloadBytes,
                createPacket,
                onSent));
        }

        recipient.BatchOffsets[fairnessKey] = (offset + 1) % batches.Count;
        return result;
    }

    public MovementTrafficFrame EndFrame(
        string controllerId,
        int deferredSnapshots,
        float maximumDeferredAgeSeconds)
    {
        return recipients.TryGetValue(controllerId, out RecipientState recipient)
            ? recipient.TrafficBudget.ReportFrame(
                deferredSnapshots,
                maximumDeferredAgeSeconds)
            : new MovementTrafficFrame();
    }

    public void RemoveRecipient(string controllerId)
    {
        if (string.IsNullOrEmpty(controllerId)) return;
        recipients.Remove(controllerId);
    }

    public void Clear()
    {
        foreach (RecipientState recipient in recipients.Values)
            recipient.TrafficBudget.Clear();
        recipients.Clear();
    }

    private RecipientState GetOrCreateRecipient(string controllerId)
    {
        if (string.IsNullOrEmpty(controllerId))
            throw new ArgumentException("A movement recipient is required.", nameof(controllerId));

        if (recipients.TryGetValue(controllerId, out RecipientState recipient))
            return recipient;

        IMovementTrafficBudget trafficBudget = trafficBudgetFactory();
        if (trafficBudget == null)
            throw new InvalidOperationException("The movement traffic-budget factory returned null.");

        recipient = new RecipientState(trafficBudget);
        recipients.Add(controllerId, recipient);
        return recipient;
    }

    private MovementSendResult SendBatch<T>(
        string controllerId,
        RecipientState recipient,
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
        int offset = recipient.SendOffsets.TryGetValue(fairnessKey, out int previousOffset)
            ? previousOffset % batch.Data.Count
            : 0;
        MovementBatch<T> orderedBatch = Rotate(batch, offset);

        var fallbackKey = (typeof(T), batch.IdentityScopeId);
        MovementIdFormat idFormat =
            batch.IdentityScopeId == null || recipient.CanonicalIdFallbacks.Contains(fallbackKey)
                ? MovementIdFormat.Canonical
                : MovementIdFormat.Compact;
        bool probeForGrowth = true;
        int sentCount = 0;

        for (int start = 0; start < orderedBatch.Data.Count;)
        {
            int availablePayloadBytes = Math.Min(
                maxPayloadBytes,
                recipient.TrafficBudget.AvailableBytes);
            if (availablePayloadBytes <= 0) break;

            int remaining = orderedBatch.Data.Count - start;
            var preferenceKey = (typeof(T), batch.IdentityScopeId, idFormat);
            int preferredCount = recipient.PreferredBatchSizes.TryGetValue(
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
                    recipient.CanonicalIdFallbacks.Add(fallbackKey);
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

            if (!recipient.TrafficBudget.TrySpend(candidate.Payload.Length)) break;
            client.Send(controllerId, candidate.Packet, candidate.Payload);
            for (int i = 0; i < candidate.Count; i++)
                onSent?.Invoke(
                    orderedBatch.CanonicalIds[start + i],
                    orderedBatch.Data[start + i]);

            if (availablePayloadBytes == maxPayloadBytes)
                RememberPreferredBatchSize(
                    recipient,
                    preferenceKey,
                    candidate.Count,
                    remaining);
            sentCount += candidate.Count;
            start += candidate.Count;
            probeForGrowth = false;
        }

        if (batch.Data.Count > 0)
            recipient.SendOffsets[fairnessKey] =
                (offset + sentCount) % batch.Data.Count;

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

    private static void RememberPreferredBatchSize(
        RecipientState recipient,
        (Type SnapshotType, string IdentityScopeId, MovementIdFormat IdFormat) preferenceKey,
        int safeCount,
        int remaining)
    {
        if (safeCount < remaining ||
            !recipient.PreferredBatchSizes.TryGetValue(
                preferenceKey,
                out int previousSafeCount) ||
            safeCount > previousSafeCount)
        {
            recipient.PreferredBatchSizes[preferenceKey] = safeCount;
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
