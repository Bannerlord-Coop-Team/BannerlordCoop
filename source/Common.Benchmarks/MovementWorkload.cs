using Common.Network;
using Common.Serialization;
using K4os.Compression.LZ4;
using LiteNetLib;
using ProtoBuf.Meta;
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
/// One frame of movement sends to each recipient: the candidate search from MovementBatchSender and the LZ4 step from
/// MovementPacketCompressor, over shape packets that match the MovementPacket field layout. With encodeOnce it takes
/// the #4487 path, which encodes each agent once per frame and splices the cached bytes into every candidate.
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
    private readonly int[] preferredCounts;
    private readonly bool encodeOnce;
    // Each agent's encoding for the current frame, back to back; offset -1 until a candidate first needs it.
    private readonly MemoryStream agentEncodings = new MemoryStream();
    private readonly int[] encodedOffsets;
    private readonly int[] encodedLengths;
    private byte[]? envelopeTypeField;

    public MovementWorkload(ICommonSerializer serializer, MovementShapeAgent[] agents, bool compactIds, int payloadBudget, int recipients = 1, bool encodeOnce = false)
    {
        this.serializer = serializer;
        this.agents = agents;
        this.compactIds = compactIds;
        this.payloadBudget = payloadBudget;
        this.encodeOnce = encodeOnce;
        preferredCounts = Enumerable.Repeat(InitialBatchSize, recipients).ToArray();
        encodedOffsets = new int[agents.Length];
        encodedLengths = new int[agents.Length];
    }

    public MovementFrameStats Stats { get; private set; } = new MovementFrameStats();

    public void ResetStats() => Stats = new MovementFrameStats();

    public void SendFrame()
    {
        Stats.Frames++;
        agentEncodings.SetLength(0);
        for (int i = 0; i < encodedOffsets.Length; i++) encodedOffsets[i] = -1;
        for (int recipient = 0; recipient < preferredCounts.Length; recipient++)
        {
            bool probeForGrowth = true;
            for (int start = 0; start < agents.Length;)
            {
                int remaining = agents.Length - start;
                (int count, byte[] payload) = FindLargestFittingBatch(start, remaining, probeForGrowth, preferredCounts[recipient]);
                if (payload.Length > payloadBudget)
                {
                    start++;
                    continue;
                }

                Stats.Packets++;
                Stats.WireBytes += payload.Length;
                foreach (byte b in payload) Stats.WireHash = (Stats.WireHash ^ b) * 1099511628211UL;
                if (count < remaining || count > preferredCounts[recipient]) preferredCounts[recipient] = count;
                start += count;
                probeForGrowth = false;
            }
        }
    }

    // Same largest-first search as MovementBatchSender.FindLargestFittingBatch.
    private (int Count, byte[] Payload) FindLargestFittingBatch(int start, int remaining, bool probeForGrowth, int preferredCount)
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
        return encodeOnce ? CompressFromCache(start, count, packet) : Compress(packet);
    }

    // Same steps as MovementPacketCompressor.SerializeSnapshots.
    private byte[] CompressFromCache(int start, int count, MovementShapePacket packet)
    {
        long t0 = Stopwatch.GetTimestamp();
        int snapshotBytes = 0;
        for (int i = start; i < start + count; i++)
        {
            if (encodedOffsets[i] < 0)
            {
                encodedOffsets[i] = (int)agentEncodings.Length;
                RuntimeTypeModel.Default.Serialize(agentEncodings, agents[i]);
                encodedLengths[i] = (int)agentEncodings.Length - encodedOffsets[i];
            }
            snapshotBytes += 1 + VarintLength((uint)encodedLengths[i]) + encodedLengths[i];
        }

        var header = new MovementShapePacket(packet.IdentityScopeId, packet.AgentIds, null!, packet.AgentGuids, packet.SenderControllerId, packet.AuthorityRevisions, packet.SampleSequence);
        byte[] headerBytes = serializer.Serialize(header);
        ReadWrapper(headerBytes, out int typeFieldLength, out int bodyOffset, out int bodyLength);
        int split = bodyOffset;
        while (split < bodyOffset + bodyLength)
        {
            int fieldStart = split;
            ulong tag = ReadVarint(headerBytes, ref split);
            if ((tag >> 3) > 3)
            {
                split = fieldStart;
                break;
            }
            int value = (int)ReadVarint(headerBytes, ref split);
            if ((tag & 7) == 2) split += value;
        }

        int packetBodyLength = bodyLength + snapshotBytes;
        int length = typeFieldLength + 1 + VarintLength((uint)packetBodyLength) + packetBodyLength;
        byte[] original = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            Buffer.BlockCopy(headerBytes, 0, original, 0, typeFieldLength);
            int offset = typeFieldLength;
            original[offset++] = 0x12;
            offset = WriteVarint(original, offset, (uint)packetBodyLength);
            Buffer.BlockCopy(headerBytes, bodyOffset, original, offset, split - bodyOffset);
            offset += split - bodyOffset;
            byte[] encoded = agentEncodings.GetBuffer();
            for (int i = start; i < start + count; i++)
            {
                original[offset++] = 0x1A;
                offset = WriteVarint(original, offset, (uint)encodedLengths[i]);
                Buffer.BlockCopy(encoded, encodedOffsets[i], original, offset, encodedLengths[i]);
                offset += encodedLengths[i];
            }
            Buffer.BlockCopy(headerBytes, split, original, offset, bodyOffset + bodyLength - split);
            Stats.SerializeTicks += Stopwatch.GetTimestamp() - t0;
            return CompressDirect(original, length);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(original);
        }
    }

    // Same steps as MovementPacketCompressor.Compress, which writes the envelope without protobuf-net.
    private byte[] CompressDirect(byte[] original, int length)
    {
        if (length == 0 || length > MaxUncompressedBytes) return original.AsSpan(0, length).ToArray();

        int maximumLength = LZ4Codec.MaximumOutputSize(length);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maximumLength);
        try
        {
            long t1 = Stopwatch.GetTimestamp();
            int compressedLength = LZ4Codec.Encode(original, 0, length, buffer, 0, maximumLength, LZ4Level.L00_FAST);
            Stats.Lz4Ticks += Stopwatch.GetTimestamp() - t1;
            if (compressedLength <= 0 || compressedLength >= length) return original.AsSpan(0, length).ToArray();

            long t2 = Stopwatch.GetTimestamp();
            if (envelopeTypeField == null)
            {
                byte[] sample = serializer.Serialize(new CompressedMovementShape(1, new byte[1]));
                ReadWrapper(sample, out int sampleTypeFieldLength, out _, out _);
                envelopeTypeField = sample.AsSpan(0, sampleTypeFieldLength).ToArray();
            }
            int bodyLength = 1 + VarintLength((uint)length) + 1 + VarintLength((uint)compressedLength) + compressedLength;
            int envelopeLength = envelopeTypeField.Length + 1 + VarintLength((uint)bodyLength) + bodyLength;
            if (envelopeLength >= length)
            {
                Stats.SerializeTicks += Stopwatch.GetTimestamp() - t2;
                return original.AsSpan(0, length).ToArray();
            }

            var envelope = new byte[envelopeLength];
            envelopeTypeField.CopyTo(envelope, 0);
            int offset = envelopeTypeField.Length;
            envelope[offset++] = 0x12;
            offset = WriteVarint(envelope, offset, (uint)bodyLength);
            envelope[offset++] = 0x08;
            offset = WriteVarint(envelope, offset, (uint)length);
            envelope[offset++] = 0x12;
            offset = WriteVarint(envelope, offset, (uint)compressedLength);
            Buffer.BlockCopy(buffer, 0, envelope, offset, compressedLength);
            Stats.SerializeTicks += Stopwatch.GetTimestamp() - t2;
            Stats.Envelopes++;
            return envelope;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    // ProtoMessageWrapper: an optional TypeId varint, then the length-prefixed Data field.
    private static void ReadWrapper(byte[] wrapped, out int typeFieldLength, out int bodyOffset, out int bodyLength)
    {
        int offset = 0;
        if (wrapped[0] != 0x12)
        {
            offset++;
            ReadVarint(wrapped, ref offset);
        }
        typeFieldLength = offset++;
        bodyLength = (int)ReadVarint(wrapped, ref offset);
        bodyOffset = offset;
    }

    private static ulong ReadVarint(byte[] data, ref int offset)
    {
        ulong value = 0;
        for (int shift = 0; ; shift += 7)
        {
            byte b = data[offset++];
            value |= (ulong)(b & 0x7F) << shift;
            if (b < 0x80) return value;
        }
    }

    private static int VarintLength(uint value)
    {
        int length = 1;
        for (; value >= 0x80; value >>= 7) length++;
        return length;
    }

    private static int WriteVarint(byte[] destination, int offset, uint value)
    {
        for (; value >= 0x80; value >>= 7) destination[offset++] = (byte)(value | 0x80);
        destination[offset++] = (byte)value;
        return offset;
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
