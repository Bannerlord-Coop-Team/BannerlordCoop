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
    int AvailableOutgoingBytes { get; }

    void BeginFrame(float elapsedSeconds);

    void ConfigureRecipient(
        string controllerId,
        int bytesPerSecond,
        int maxPayloadBytes);

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
    internal int ProcessedCount { get; }

    public MovementSendResult(
        int sentCount,
        int deferredCount,
        int processedCount = -1)
    {
        SentCount = sentCount;
        DeferredCount = deferredCount;
        ProcessedCount = processedCount < 0 ? sentCount : processedCount;
    }

    public MovementSendResult Add(MovementSendResult other) =>
        new MovementSendResult(
            SentCount + other.SentCount,
            DeferredCount + other.DeferredCount,
            ProcessedCount + other.ProcessedCount);
}

public sealed class MovementBatch<T>
{
    public string IdentityScopeId { get; }
    public bool IsPriority { get; }
    public List<ushort> CompactIds { get; } = new List<ushort>();
    public List<Guid> CanonicalIds { get; } = new List<Guid>();
    public List<T> Data { get; } = new List<T>();
    public List<MovementPriorityKey> Priorities { get; } = new List<MovementPriorityKey>();

    public bool HasPriorities => Priorities.Count == Data.Count && Data.Count > 0;

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

    public void Add(CoopAgentInfo info, T data, MovementPriorityKey priority)
    {
        Add(info, data);
        Priorities.Add(priority);
    }

    public void Clear()
    {
        CompactIds.Clear();
        CanonicalIds.Clear();
        Data.Clear();
        Priorities.Clear();
    }
}

/// <summary>Selects and sends the largest movement batches that fit each recipient's route budget.</summary>
public sealed class MovementBatchSender : IMovementBatchSender
{
    private static readonly ILogger Logger = LogManager.GetLogger<MovementBatchSender>();
    private const int InitialBatchSize = 3;

    private readonly IBattleNetwork client;
    private readonly IMovementPacketCompressor packetCompressor;
    private readonly IMovementTrafficBudgetFactory trafficBudgetFactory;
    private readonly IMovementPriorityScheduler priorityScheduler;
    private readonly IMovementNetworkSettings networkSettings;
    private readonly IMovementTrafficBudget outgoingTrafficBudget;
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

    /// <summary>Tracks progress through one reusable priority batch without copying its unsent tail.</summary>
    private sealed class PrioritizedBatchCursor<T>
    {
        public MovementBatch<T> Batch { get; }
        public int StartIndex { get; set; }

        public PrioritizedBatchCursor(MovementBatch<T> batch)
        {
            Batch = batch;
        }
    }

    public MovementBatchSender(
        IBattleNetwork client,
        IMovementPacketCompressor packetCompressor,
        IMovementTrafficBudgetFactory trafficBudgetFactory,
        IMovementPriorityScheduler priorityScheduler,
        IMovementNetworkSettings networkSettings)
    {
        if (client == null) throw new ArgumentNullException(nameof(client));
        if (packetCompressor == null) throw new ArgumentNullException(nameof(packetCompressor));
        if (trafficBudgetFactory == null) throw new ArgumentNullException(nameof(trafficBudgetFactory));
        if (priorityScheduler == null) throw new ArgumentNullException(nameof(priorityScheduler));
        if (networkSettings == null) throw new ArgumentNullException(nameof(networkSettings));

        this.client = client;
        this.packetCompressor = packetCompressor;
        this.trafficBudgetFactory = trafficBudgetFactory;
        this.priorityScheduler = priorityScheduler;
        this.networkSettings = networkSettings;
        outgoingTrafficBudget = trafficBudgetFactory.Create(
            networkSettings.OutgoingBytesPerSecond,
            CalculateBurstBytes(networkSettings.OutgoingBytesPerSecond, 1000));
        if (outgoingTrafficBudget == null)
            throw new InvalidOperationException("The movement traffic-budget factory returned null.");
    }

    internal MovementBatchSender(
        IBattleNetwork client,
        IMovementPacketCompressor packetCompressor,
        Func<IMovementTrafficBudget> trafficBudgetFactory)
        : this(
            client,
            packetCompressor,
            new DelegateMovementTrafficBudgetFactory(trafficBudgetFactory),
            new MovementPriorityScheduler(),
            new MovementNetworkSettings(1d, 1d))
    {
    }

    public int AvailableOutgoingBytes => outgoingTrafficBudget.AvailableBytes;

    public void BeginFrame(float elapsedSeconds)
    {
        outgoingTrafficBudget.Advance(elapsedSeconds);
        foreach (RecipientState recipient in recipients.Values)
            recipient.TrafficBudget.Advance(elapsedSeconds);
    }

    public void ConfigureRecipient(
        string controllerId,
        int bytesPerSecond,
        int maxPayloadBytes)
    {
        RecipientState recipient = GetOrCreateRecipient(controllerId);
        recipient.TrafficBudget.Configure(
            Math.Max(1, bytesPerSecond),
            CalculateBurstBytes(bytesPerSecond, maxPayloadBytes));
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
                batches.Add(OrderByPriority(batch));
        }
        if (legacyBatch != null && legacyBatch.Data.Count > 0)
            batches.Add(OrderByPriority(legacyBatch));

        if (batches.Count == 0) return new MovementSendResult();

        bool prioritize = batches.Exists(batch => batch.HasPriorities);
        if (prioritize)
        {
            batches.Sort((left, right) => CompareBatchPriority(left, right));
        }

        if (prioritize)
        {
            return SendPrioritizedBatches(
                controllerId,
                recipient,
                batches,
                maxPayloadBytes,
                createPacket,
                onSent);
        }

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

    private MovementSendResult SendPrioritizedBatches<T>(
        string controllerId,
        RecipientState recipient,
        List<MovementBatch<T>> batches,
        int maxPayloadBytes,
        Func<string, ushort[], Guid[], T[], IPacket> createPacket,
        Action<Guid, T> onSent)
    {
        int totalSnapshots = 0;
        foreach (MovementBatch<T> batch in batches)
            totalSnapshots += batch.Data.Count;

        int sentSnapshots = 0;
        var pending = new List<PrioritizedBatchCursor<T>>(batches.Count);
        foreach (MovementBatch<T> batch in batches)
            pending.Add(new PrioritizedBatchCursor<T>(batch));
        var probedScopes = new HashSet<string>(StringComparer.Ordinal);
        while (pending.Count > 0)
        {
            pending.Sort((left, right) => CompareBatchPriority(left, right));
            PrioritizedBatchCursor<T> cursor = pending[0];
            MovementBatch<T> batch = cursor.Batch;
            MovementSendResult result = SendBatch(
                controllerId,
                recipient,
                batch,
                maxPayloadBytes,
                createPacket,
                onSent,
                maximumPackets: 1,
                allowProbeForGrowth: probedScopes.Add(batch.IdentityScopeId),
                startOffset: cursor.StartIndex);
            if (result.ProcessedCount <= 0) break;

            sentSnapshots += result.SentCount;
            cursor.StartIndex += result.ProcessedCount;
            if (cursor.StartIndex >= batch.Data.Count)
                pending.RemoveAt(0);
        }

        return new MovementSendResult(
            sentSnapshots,
            Math.Max(0, totalSnapshots - sentSnapshots));
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
        outgoingTrafficBudget.Clear();
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

        IMovementTrafficBudget trafficBudget = trafficBudgetFactory.Create(
            networkSettings.OutgoingBytesPerSecond,
            CalculateBurstBytes(networkSettings.OutgoingBytesPerSecond, 1000));
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
        Action<Guid, T> onSent,
        int maximumPackets = int.MaxValue,
        bool allowProbeForGrowth = true,
        int startOffset = 0)
    {
        if (batch == null) return new MovementSendResult();
        if (batch.Data.Count == 0) return new MovementSendResult();
        if (maxPayloadBytes <= 0)
        {
            LogMissingPayloadBudget(batch);
            return new MovementSendResult(0, batch.Data.Count);
        }

        var fairnessKey = (typeof(T), batch.IdentityScopeId, batch.IsPriority);
        bool prioritize = batch.HasPriorities;
        int offset = prioritize
            ? 0
            : recipient.SendOffsets.TryGetValue(fairnessKey, out int previousOffset)
                ? previousOffset % batch.Data.Count
                : 0;
        var fallbackKey = (typeof(T), batch.IdentityScopeId);
        MovementIdFormat idFormat =
            batch.IdentityScopeId == null || recipient.CanonicalIdFallbacks.Contains(fallbackKey)
                ? MovementIdFormat.Canonical
                : MovementIdFormat.Compact;
        bool probeForGrowth = allowProbeForGrowth;
        int sentCount = 0;
        int processedCount = 0;
        int sentPackets = 0;

        for (int start = startOffset;
            start < batch.Data.Count && sentPackets < maximumPackets;)
        {
            int availablePayloadBytes = Math.Min(
                maxPayloadBytes,
                Math.Min(
                    outgoingTrafficBudget.AvailableBytes,
                    recipient.TrafficBudget.AvailableBytes));
            if (availablePayloadBytes <= 0) break;

            int remaining = batch.Data.Count - start;
            var preferenceKey = (typeof(T), batch.IdentityScopeId, idFormat);
            int preferredCount = recipient.PreferredBatchSizes.TryGetValue(
                preferenceKey, out int previousSafeCount)
                ? previousSafeCount
                : InitialBatchSize;

            SerializedMovementBatch candidate = FindLargestFittingBatch(
                batch,
                offset,
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
                    batch,
                    offset,
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
                        GetCircular(
                            batch.CanonicalIds,
                            offset,
                            start),
                        candidate,
                        canonicalCandidate,
                        availablePayloadBytes);
                    start++;
                    processedCount++;
                    continue;
                }
            }

            if (!candidate.Fits(availablePayloadBytes))
            {
                LogOversizedCanonicalSnapshot(
                    GetCircular(
                        batch.CanonicalIds,
                        offset,
                        start),
                    candidate,
                    availablePayloadBytes);
                start++;
                processedCount++;
                continue;
            }

            if (!recipient.TrafficBudget.TrySpend(candidate.Payload.Length)) break;
            if (!outgoingTrafficBudget.TrySpend(candidate.Payload.Length))
                throw new InvalidOperationException("The shared movement budget changed during a game-thread send.");
            client.Send(controllerId, candidate.Packet, candidate.Payload);
            for (int i = 0; i < candidate.Count; i++)
                onSent?.Invoke(
                    GetCircular(
                        batch.CanonicalIds,
                        offset,
                        start + i),
                    GetCircular(
                        batch.Data,
                        offset,
                        start + i));

            if (availablePayloadBytes == maxPayloadBytes)
                RememberPreferredBatchSize(
                    recipient,
                    preferenceKey,
                    candidate.Count,
                    remaining);
            sentCount += candidate.Count;
            processedCount += candidate.Count;
            sentPackets++;
            start += candidate.Count;
            probeForGrowth = false;
        }

        if (!prioritize && batch.Data.Count > 0)
            recipient.SendOffsets[fairnessKey] =
                (offset + sentCount) % batch.Data.Count;

        return new MovementSendResult(
            sentCount,
            Math.Max(0, batch.Data.Count - startOffset - sentCount),
            processedCount);
    }

    private MovementBatch<T> OrderByPriority<T>(MovementBatch<T> batch)
    {
        if (!batch.HasPriorities || batch.Data.Count < 2) return batch;

        bool alreadyOrdered = true;
        for (int i = 1; i < batch.Priorities.Count; i++)
        {
            if (priorityScheduler.Compare(batch.Priorities[i - 1], batch.Priorities[i]) <= 0)
                continue;

            alreadyOrdered = false;
            break;
        }
        if (alreadyOrdered) return batch;

        var indices = new List<int>(batch.Data.Count);
        for (int i = 0; i < batch.Data.Count; i++) indices.Add(i);
        indices.Sort((left, right) =>
            priorityScheduler.Compare(batch.Priorities[left], batch.Priorities[right]));

        var ordered = new MovementBatch<T>(batch.IdentityScopeId, batch.IsPriority);
        foreach (int source in indices)
        {
            ordered.CanonicalIds.Add(batch.CanonicalIds[source]);
            if (batch.IdentityScopeId != null)
                ordered.CompactIds.Add(batch.CompactIds[source]);
            ordered.Data.Add(batch.Data[source]);
            ordered.Priorities.Add(batch.Priorities[source]);
        }
        return ordered;
    }

    private int CompareBatchPriority<T>(MovementBatch<T> left, MovementBatch<T> right)
    {
        if (!left.HasPriorities) return right.HasPriorities ? 1 : 0;
        if (!right.HasPriorities) return -1;
        return priorityScheduler.Compare(left.Priorities[0], right.Priorities[0]);
    }

    private int CompareBatchPriority<T>(
        PrioritizedBatchCursor<T> left,
        PrioritizedBatchCursor<T> right)
    {
        return priorityScheduler.Compare(
            left.Batch.Priorities[left.StartIndex],
            right.Batch.Priorities[right.StartIndex]);
    }

    private static int CalculateBurstBytes(int bytesPerSecond, int maxPayloadBytes)
    {
        int sustainedBurst = Math.Max(1, bytesPerSecond) / 8;
        return Math.Max(Math.Max(1, maxPayloadBytes), sustainedBurst);
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
        int offset,
        int start,
        int preferredCount,
        bool probeForGrowth,
        int maxPayloadBytes,
        MovementIdFormat idFormat,
        Func<string, ushort[], Guid[], T[], IPacket> createPacket)
    {
        int remaining = batch.Data.Count - start;

        SerializedMovementBatch CreateCandidate(int count) =>
            CreateSerializedCandidate(
                batch,
                offset,
                start,
                count,
                idFormat,
                createPacket);

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
        int offset,
        int start,
        int count,
        MovementIdFormat idFormat,
        Func<string, ushort[], Guid[], T[], IPacket> createPacket)
    {
        var data = new T[count];
        CopyCircular(batch.Data, offset, start, data);

        IPacket packet;
        if (idFormat == MovementIdFormat.Canonical)
        {
            var ids = new Guid[count];
            CopyCircular(batch.CanonicalIds, offset, start, ids);
            packet = createPacket(null, null, ids, data);
        }
        else
        {
            var ids = new ushort[count];
            CopyCircular(batch.CompactIds, offset, start, ids);
            packet = createPacket(batch.IdentityScopeId, ids, null, data);
        }

        return new SerializedMovementBatch(
            count,
            packet,
            packetCompressor.Serialize(packet));
    }

    private static T GetCircular<T>(
        List<T> source,
        int offset,
        int logicalIndex)
    {
        return source[(offset + logicalIndex) % source.Count];
    }

    private static void CopyCircular<T>(
        List<T> source,
        int offset,
        int logicalStart,
        T[] destination)
    {
        for (int i = 0; i < destination.Length; i++)
            destination[i] = GetCircular(source, offset, logicalStart + i);
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
