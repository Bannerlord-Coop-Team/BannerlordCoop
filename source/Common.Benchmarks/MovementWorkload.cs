using Common.Network;
using Common.Serialization;
using K4os.Compression.LZ4;
using LiteNetLib;
using System.Buffers;
using System.Diagnostics;

namespace Common.Benchmarks;

/// <summary>Counts, timings and wire hash per workload; legacy and current must match on counts and hash.</summary>
internal sealed class MovementFrameStats
{
    public long Frames;
    public int Candidates;
    public int Envelopes;
    public int Packets;
    public long WireBytes;
    public ulong WireHash = 14695981039346656037UL;
    public long SerializeTicks;
    public long Lz4Ticks;
}

/// <summary>
/// One frame of movement sends: the candidate search from MovementBatchSender and the LZ4 step from
/// MovementPacketCompressor, over shape packets that match the MovementPacket field layout.
/// </summary>
internal sealed class MovementWorkload
{
    // MovementPacketCompressor.MaxUncompressedBytes and MovementBatchSender.InitialBatchSize.
    private const int MaxUncompressedBytes = 4096;
    private const int InitialBatchSize = 3;

    private readonly ICommonSerializer serializer;
    private readonly MovementShapeAgent[] agents;
    private readonly bool compactIds;
    private readonly int payloadBudget;
    private int preferredCount = InitialBatchSize;

    public MovementWorkload(ICommonSerializer serializer, MovementShapeAgent[] agents, bool compactIds, int payloadBudget)
    {
        this.serializer = serializer;
        this.agents = agents;
        this.compactIds = compactIds;
        this.payloadBudget = payloadBudget;
    }

    public MovementFrameStats Stats { get; private set; } = new MovementFrameStats();

    public void ResetStats() => Stats = new MovementFrameStats();

    public void SendFrame()
    {
        Stats.Frames++;
        bool probeForGrowth = true;
        for (int start = 0; start < agents.Length;)
        {
            int remaining = agents.Length - start;
            (int count, byte[] payload) = FindLargestFittingBatch(start, remaining, probeForGrowth);
            if (payload.Length > payloadBudget)
            {
                start++;
                continue;
            }

            Stats.Packets++;
            Stats.WireBytes += payload.Length;
            foreach (byte b in payload) Stats.WireHash = (Stats.WireHash ^ b) * 1099511628211UL;
            if (count < remaining || count > preferredCount) preferredCount = count;
            start += count;
            probeForGrowth = false;
        }
    }

    // Same largest-first search as MovementBatchSender.FindLargestFittingBatch.
    private (int Count, byte[] Payload) FindLargestFittingBatch(int start, int remaining, bool probeForGrowth)
    {
        int initialCount = Math.Min(Math.Max(1, preferredCount), remaining);
        var initial = (initialCount, CreateCandidate(start, initialCount));
        if (initial.Item2.Length <= payloadBudget)
        {
            if (initialCount == remaining || !probeForGrowth) return initial;

            var safe = initial;
            for (long offset = 1; ; offset *= 2)
            {
                int probeCount = (int)Math.Min(remaining, initialCount + offset);
                byte[] probe = CreateCandidate(start, probeCount);
                if (probe.Length > payloadBudget) return Refine(start, safe, probeCount);
                safe = (probeCount, probe);
                if (probeCount == remaining) return safe;
            }
        }

        if (initialCount == 1) return initial;
        int smallestOversized = initialCount;
        for (long offset = 1; ; offset *= 2)
        {
            int probeCount = (int)Math.Max(1, initialCount - offset);
            byte[] probe = CreateCandidate(start, probeCount);
            if (probe.Length <= payloadBudget) return Refine(start, (probeCount, probe), smallestOversized);
            smallestOversized = probeCount;
            if (probeCount == 1) return (probeCount, probe);
        }
    }

    private (int Count, byte[] Payload) Refine(int start, (int Count, byte[] Payload) knownSafe, int knownOversized)
    {
        int low = knownSafe.Count;
        int high = knownOversized;
        var largest = knownSafe;
        while (low + 1 < high)
        {
            int probeCount = low + ((high - low) / 2);
            byte[] probe = CreateCandidate(start, probeCount);
            if (probe.Length <= payloadBudget)
            {
                low = probeCount;
                largest = (probeCount, probe);
            }
            else
            {
                high = probeCount;
            }
        }
        return largest;
    }

    private byte[] CreateCandidate(int start, int count)
    {
        var data = new MovementShapeAgent[count];
        Array.Copy(agents, start, data, 0, count);
        var revisions = new long[count];
        for (int i = 0; i < count; i++) revisions[i] = (start + i) * 3L;
        MovementShapePacket packet = compactIds
            ? new MovementShapePacket("MapEvent_Created_0000", Enumerable.Range(start + 1, count).Select(i => (ushort)i).ToArray(), data, null, "76561198000000042", revisions, 123456)
            : new MovementShapePacket(null, null, data, Enumerable.Range(start, count).Select(BenchmarkPayloads.DeterministicGuid).ToArray(), "76561198000000042", revisions, 123456);

        Stats.Candidates++;
        return Compress(packet);
    }

    // Same steps as MovementPacketCompressor.Serialize.
    private byte[] Compress(MovementShapePacket packet)
    {
        long t0 = Stopwatch.GetTimestamp();
        byte[] original = serializer.Serialize(packet);
        Stats.SerializeTicks += Stopwatch.GetTimestamp() - t0;
        if (original.Length == 0 || original.Length > MaxUncompressedBytes) return original;

        int maximumLength = LZ4Codec.MaximumOutputSize(original.Length);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maximumLength);
        try
        {
            long t1 = Stopwatch.GetTimestamp();
            int compressedLength = LZ4Codec.Encode(original, 0, original.Length, buffer, 0, maximumLength, LZ4Level.L00_FAST);
            Stats.Lz4Ticks += Stopwatch.GetTimestamp() - t1;
            if (compressedLength <= 0 || compressedLength >= original.Length) return original;

            var payload = new byte[compressedLength];
            Buffer.BlockCopy(buffer, 0, payload, 0, compressedLength);
            long t2 = Stopwatch.GetTimestamp();
            byte[] envelope = serializer.Serialize(new CompressedMovementShape(original.Length, payload));
            Stats.SerializeTicks += Stopwatch.GetTimestamp() - t2;
            if (envelope.Length >= original.Length) return original;

            Stats.Envelopes++;
            return envelope;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    // Same search as LiteNetP2PClient.CalculateMaxRelayPayloadBytes with SafeSinglePacketBytes.
    public static int RelayPayloadBudget(ICommonSerializer serializer)
    {
        const int maxDatagramBytes = 1000;
        bool Fits(int payloadBytes) => serializer.Serialize(new RelayPacket(
            DeliveryMethod.Unreliable, "MapEvent_Created_0000", "76561198000000042", new byte[payloadBytes])).Length <= maxDatagramBytes;

        int low = 0;
        int high = maxDatagramBytes;
        while (low < high)
        {
            int candidate = low + ((high - low + 1) / 2);
            if (Fits(candidate)) low = candidate;
            else high = candidate - 1;
        }
        return low;
    }
}
