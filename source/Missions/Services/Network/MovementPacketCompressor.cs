using Common.PacketHandlers;
using Common.Serialization;
using K4os.Compression.LZ4;
using Missions.Agents.Packets;
#if DEBUG
using Missions.Diagnostics;
#endif
using ProtoBuf.Meta;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;

namespace Missions.Services.Network;

public interface IMovementPacketCompressor
{
    byte[] Serialize(IPacket packet);

    /// <summary>
    /// Same bytes as <see cref="Serialize"/>, but each snapshot is encoded once per capture and reused by every later
    /// packet of that capture. <paramref name="snapshotIds"/> holds the canonical id of each snapshot in packet order.
    /// </summary>
    byte[] SerializeSnapshots(IPacket packet, Guid[] snapshotIds);

    bool TryRestore(IPacket packet, out IPacket restored);
}

/// <summary>
/// Serializes movement packets and wraps them in an LZ4 <see cref="CompressedMovementPacket"/> when that is smaller.
/// </summary>
public class MovementPacketCompressor : IMovementPacketCompressor
{
    internal const int MaxUncompressedBytes = 4096;

    // The packets' snapshot array (field 3) and CompressedMovementPacket's two fields.
    private const int SnapshotField = 3;
    private const byte SnapshotTag = (SnapshotField << 3) | 2;
    private const byte UncompressedLengthTag = 0x08;
    private const byte CompressedPayloadTag = 0x12;

    private readonly ICommonSerializer serializer;
    // Reusable snapshot encodings for the current capture, back to back, indexed by agent id.
    private readonly MemoryStream capturedEncodings = new MemoryStream();
    private readonly Dictionary<Guid, int> agentSegments = new Dictionary<Guid, int>();
    private readonly Dictionary<Guid, int> mountSegments = new Dictionary<Guid, int>();
    private readonly List<EncodedSnapshot> capturedSegments = new List<EncodedSnapshot>();
    // Deck poses carry a per-packet hull index, so they are encoded per packet here instead.
    private readonly MemoryStream packetEncodings = new MemoryStream();
    private EncodedSnapshot[] packetSnapshots = new EncodedSnapshot[16];
    private long capturedSequence;
    private byte[] compressedTypeField;

    public MovementPacketCompressor(ICommonSerializer serializer)
    {
        this.serializer = serializer;
    }

    public byte[] Serialize(IPacket packet)
    {
        byte[] original = serializer.Serialize(packet);
#if DEBUG
        if (packet is AgentActionPacket actionPacket)
        {
            MissionActionDiagnostics.RecordActionPacketSent(
                actionPacket,
                original.Length);
        }
#endif
        if (!IsMovement(packet))
            return original;

        return Compress(original, original.Length, originalIsOwned: true);
    }

    public byte[] SerializeSnapshots(IPacket packet, Guid[] snapshotIds)
    {
        // Sequence 0 is never a real capture, so it gives no guarantee that an id maps to one snapshot.
        switch (packet)
        {
            case MovementPacket movement
                when movement.SampleSequence != 0 && HasIdPerSnapshot(movement.Agents, snapshotIds):
                return SerializeWithSnapshots(
                    movement.WithoutAgents(),
                    movement.SampleSequence,
                    movement.Agents,
                    snapshotIds,
                    agentSegments,
                    agent => agent.DeckShipIndex == 0);
            case MountMovementPacket mounts
                when mounts.SampleSequence != 0 && HasIdPerSnapshot(mounts.Mounts, snapshotIds):
                return SerializeWithSnapshots(
                    mounts.WithoutMounts(),
                    mounts.SampleSequence,
                    mounts.Mounts,
                    snapshotIds,
                    mountSegments,
                    mount => true);
            default:
                return Serialize(packet);
        }
    }

    public bool TryRestore(IPacket packet, out IPacket restored)
    {
        if (!(packet is CompressedMovementPacket compressed))
        {
            restored = packet;
            return true;
        }

        restored = null;
        if (compressed.Payload == null ||
            compressed.UncompressedLength <= 0 ||
            compressed.UncompressedLength > MaxUncompressedBytes)
        {
            return false;
        }

        var decompressed = new byte[compressed.UncompressedLength];
        try
        {
            int decodedLength = LZ4Codec.Decode(
                compressed.Payload, 0, compressed.Payload.Length,
                decompressed, 0, decompressed.Length);
            if (decodedLength != decompressed.Length)
                return false;

            restored = serializer.Deserialize<IPacket>(decompressed);
            return restored != null && IsMovement(restored);
        }
        catch (Exception)
        {
            restored = null;
            return false;
        }
    }

    private static bool HasIdPerSnapshot<T>(T[] snapshots, Guid[] snapshotIds) =>
        snapshots != null && snapshots.Length > 0 &&
        snapshotIds != null && snapshotIds.Length == snapshots.Length;

    // Writes the bytes Serialize would write for the packet, splicing cached snapshot encodings in as field 3.
    private byte[] SerializeWithSnapshots<T>(
        IPacket packetWithoutSnapshots,
        long sampleSequence,
        T[] snapshots,
        Guid[] snapshotIds,
        Dictionary<Guid, int> segmentsById,
        Func<T, bool> isReusable)
    {
        if (sampleSequence != capturedSequence)
        {
            capturedSequence = sampleSequence;
            capturedEncodings.SetLength(0);
            capturedSegments.Clear();
            agentSegments.Clear();
            mountSegments.Clear();
        }
        packetEncodings.SetLength(0);
        if (packetSnapshots.Length < snapshots.Length)
            packetSnapshots = new EncodedSnapshot[Math.Max(snapshots.Length, packetSnapshots.Length * 2)];

        int snapshotBytes = 0;
        for (int i = 0; i < snapshots.Length; i++)
        {
            EncodedSnapshot encoded;
            if (!isReusable(snapshots[i]))
            {
                encoded = Encode(packetEncodings, snapshots[i], captured: false);
            }
            else if (segmentsById.TryGetValue(snapshotIds[i], out int segment))
            {
                encoded = capturedSegments[segment];
            }
            else
            {
                encoded = Encode(capturedEncodings, snapshots[i], captured: true);
                segmentsById.Add(snapshotIds[i], capturedSegments.Count);
                capturedSegments.Add(encoded);
            }

            packetSnapshots[i] = encoded;
            snapshotBytes += 1 + ProtoBufSerializer.GetVarintLength((uint)encoded.Length) + encoded.Length;
        }

        byte[] header = serializer.Serialize(packetWithoutSnapshots);
        ProtoBufSerializer.ReadWrapper(header, out int typeFieldLength, out int bodyOffset, out int bodyLength);
        int split = FindFirstFieldAfter(header, bodyOffset, bodyOffset + bodyLength, SnapshotField);
        int packetBodyLength = bodyLength + snapshotBytes;
        int length = typeFieldLength + 1 + ProtoBufSerializer.GetVarintLength((uint)packetBodyLength) + packetBodyLength;

        byte[] buffer = ArrayPool<byte>.Shared.Rent(length);
        try
        {
            Buffer.BlockCopy(header, 0, buffer, 0, typeFieldLength);
            int offset = typeFieldLength;
            buffer[offset++] = ProtoBufSerializer.DataTag;
            offset = ProtoBufSerializer.WriteVarint(buffer, offset, (uint)packetBodyLength);
            Buffer.BlockCopy(header, bodyOffset, buffer, offset, split - bodyOffset);
            offset += split - bodyOffset;

            byte[] captured = capturedEncodings.GetBuffer();
            byte[] perPacket = packetEncodings.GetBuffer();
            for (int i = 0; i < snapshots.Length; i++)
            {
                EncodedSnapshot encoded = packetSnapshots[i];
                buffer[offset++] = SnapshotTag;
                offset = ProtoBufSerializer.WriteVarint(buffer, offset, (uint)encoded.Length);
                Buffer.BlockCopy(encoded.Captured ? captured : perPacket, encoded.Offset, buffer, offset, encoded.Length);
                offset += encoded.Length;
            }

            int tailLength = bodyOffset + bodyLength - split;
            Buffer.BlockCopy(header, split, buffer, offset, tailLength);
            return Compress(buffer, length, originalIsOwned: false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static EncodedSnapshot Encode<T>(MemoryStream destination, T snapshot, bool captured)
    {
        int start = (int)destination.Length;
        RuntimeTypeModel.Default.Serialize(destination, snapshot);
        return new EncodedSnapshot(captured, start, (int)destination.Length - start);
    }

    // Returns the LZ4 envelope when it is smaller than the first length bytes of original, otherwise those bytes.
    // A rented original goes back to the pool, so its bytes are always copied out.
    private byte[] Compress(byte[] original, int length, bool originalIsOwned)
    {
        if (length == 0 || length > MaxUncompressedBytes)
            return Exact(original, length, originalIsOwned);

        int maximumLength = LZ4Codec.MaximumOutputSize(length);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maximumLength);
        try
        {
            int compressedLength = LZ4Codec.Encode(
                original, 0, length,
                buffer, 0, maximumLength,
                LZ4Level.L00_FAST);
            if (compressedLength <= 0 || compressedLength >= length)
                return Exact(original, length, originalIsOwned);

            return WriteEnvelope(length, buffer, compressedLength) ?? Exact(original, length, originalIsOwned);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    // Writes the bytes Serialize(new CompressedMovementPacket(...)) would, or null when they are not smaller.
    private byte[] WriteEnvelope(int uncompressedLength, byte[] payload, int payloadLength)
    {
        byte[] typeField = compressedTypeField ??= ReadTypeField(
            serializer.Serialize(new CompressedMovementPacket(1, new byte[1])));
        int bodyLength = 1 + ProtoBufSerializer.GetVarintLength((uint)uncompressedLength) +
            1 + ProtoBufSerializer.GetVarintLength((uint)payloadLength) + payloadLength;
        int length = typeField.Length + 1 + ProtoBufSerializer.GetVarintLength((uint)bodyLength) + bodyLength;
        if (length >= uncompressedLength) return null;

        var envelope = new byte[length];
        Buffer.BlockCopy(typeField, 0, envelope, 0, typeField.Length);
        int offset = typeField.Length;
        envelope[offset++] = ProtoBufSerializer.DataTag;
        offset = ProtoBufSerializer.WriteVarint(envelope, offset, (uint)bodyLength);
        envelope[offset++] = UncompressedLengthTag;
        offset = ProtoBufSerializer.WriteVarint(envelope, offset, (uint)uncompressedLength);
        envelope[offset++] = CompressedPayloadTag;
        offset = ProtoBufSerializer.WriteVarint(envelope, offset, (uint)payloadLength);
        Buffer.BlockCopy(payload, 0, envelope, offset, payloadLength);
        return envelope;
    }

    private static byte[] Exact(byte[] source, int length, bool sourceIsOwned)
    {
        if (sourceIsOwned && source.Length == length) return source;

        var exact = new byte[length];
        Buffer.BlockCopy(source, 0, exact, 0, length);
        return exact;
    }

    private static byte[] ReadTypeField(byte[] wrapped)
    {
        ProtoBufSerializer.ReadWrapper(wrapped, out int typeFieldLength, out _, out _);
        var typeField = new byte[typeFieldLength];
        Buffer.BlockCopy(wrapped, 0, typeField, 0, typeFieldLength);
        return typeField;
    }

    // protobuf-net writes fields in number order, so the snapshot field goes before the first higher-numbered one.
    private static int FindFirstFieldAfter(byte[] data, int offset, int end, int fieldNumber)
    {
        while (offset < end)
        {
            int fieldStart = offset;
            ulong tag = ProtoBufSerializer.ReadVarint(data, ref offset);
            if ((int)(tag >> 3) > fieldNumber) return fieldStart;

            switch ((int)(tag & 7))
            {
                case 0:
                    ProtoBufSerializer.ReadVarint(data, ref offset);
                    break;
                case 1:
                    offset += 8;
                    break;
                case 2:
                    int fieldLength = checked((int)ProtoBufSerializer.ReadVarint(data, ref offset));
                    offset += fieldLength;
                    break;
                case 5:
                    offset += 4;
                    break;
                default:
                    throw new InvalidOperationException($"Unexpected wire type in movement packet tag {tag}.");
            }
        }
        return end;
    }

    private static bool IsMovement(IPacket packet)
    {
        return packet.PacketType == PacketType.Movement ||
               packet.PacketType == PacketType.MountMovement;
    }

    private readonly struct EncodedSnapshot
    {
        public readonly bool Captured;
        public readonly int Offset;
        public readonly int Length;

        public EncodedSnapshot(bool captured, int offset, int length)
        {
            Captured = captured;
            Offset = offset;
            Length = length;
        }
    }
}
