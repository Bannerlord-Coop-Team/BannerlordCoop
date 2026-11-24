using Common.PacketHandlers;
using Common.Serialization;
using K4os.Compression.LZ4;
using Missions.Agents.Packets;
#if DEBUG
using Missions.Diagnostics;
#endif
using System;
using System.Buffers;

namespace Missions.Services.Network;

public interface IMovementPacketCompressor
{
    byte[] Serialize(IPacket packet);

    bool TryRestore(IPacket packet, out IPacket restored);

    /// <summary>The bytes <see cref="Serialize"/> writes for <paramref name="packet"/>, worked out without serializing it.</summary>
    int GetSerializedLength(CompressedMovementPacket packet);
}

/// <summary>
/// Serializes movement packets and wraps them in an LZ4 <see cref="CompressedMovementPacket"/> when that is smaller.
/// </summary>
public class MovementPacketCompressor : IMovementPacketCompressor
{
    internal const int MaxUncompressedBytes = 4096;

    private const byte UncompressedLengthTag = 0x08;
    private const byte CompressedPayloadTag = 0x12;

    private readonly ICommonSerializer serializer;
    private byte[] compressedTypeField;

    public MovementPacketCompressor(ICommonSerializer serializer)
    {
        this.serializer = serializer;
    }

    public byte[] Serialize(IPacket packet)
    {
        return TrySerializePacked(packet) ?? SerializeUnpacked(packet);
    }

    private byte[] SerializeUnpacked(IPacket packet)
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

        return TryCompress(original) ?? original;
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

            switch (serializer.Deserialize<IPacket>(decompressed))
            {
                case MovementPacket movement:
                    restored = movement.PackedSnapshots == null
                        ? movement
                        : movement.WithRestoredSnapshots(MovementSnapshotPacking.Restore(movement));
                    return true;
                case MountMovementPacket mounts:
                    restored = mounts.PackedSnapshots == null
                        ? mounts
                        : mounts.WithRestoredSnapshots(MovementSnapshotPacking.Restore(mounts));
                    return true;
                default:
                    return false;
            }
        }
        catch (Exception)
        {
            restored = null;
            return false;
        }
    }

    private byte[] TrySerializePacked(IPacket packet)
    {
        // The packed layout goes inside the existing envelope, which restores the snapshot array before dispatch.
        byte[] original;
        switch (packet)
        {
            case MovementPacket movement:
                byte[] riders = MovementSnapshotPacking.Pack(movement, MaxUncompressedBytes);
                if (riders == null) return null;
                original = serializer.Serialize(movement.WithPackedSnapshots(riders));
                break;
            case MountMovementPacket mounts:
                byte[] horses = MovementSnapshotPacking.Pack(mounts, MaxUncompressedBytes);
                if (horses == null) return null;
                original = serializer.Serialize(mounts.WithPackedSnapshots(horses));
                break;
            default:
                return null;
        }
        return TryCompress(original);
    }

    private byte[] TryCompress(byte[] original)
    {
        int length = original.Length;
        if (length == 0 || length > MaxUncompressedBytes)
            return null;

        int maximumLength = LZ4Codec.MaximumOutputSize(length);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maximumLength);
        try
        {
            int compressedLength = LZ4Codec.Encode(
                original, 0, length,
                buffer, 0, maximumLength,
                LZ4Level.L00_FAST);
            if (compressedLength <= 0 || compressedLength >= length)
                return null;

            return WriteEnvelope(length, buffer, compressedLength);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public int GetSerializedLength(CompressedMovementPacket packet) =>
        EnvelopeLength(packet.UncompressedLength, packet.Payload?.Length ?? 0, out _);

    // The length of the bytes Serialize(new CompressedMovementPacket(...)) writes, and of their body.
    private int EnvelopeLength(int uncompressedLength, int payloadLength, out int bodyLength)
    {
        byte[] typeField = compressedTypeField ??= ReadTypeField(
            serializer.Serialize(new CompressedMovementPacket(1, new byte[1])));
        bodyLength = 1 + ProtoBufSerializer.GetVarintLength((uint)uncompressedLength) +
            1 + ProtoBufSerializer.GetVarintLength((uint)payloadLength) + payloadLength;
        return typeField.Length + 1 + ProtoBufSerializer.GetVarintLength((uint)bodyLength) + bodyLength;
    }

    // Writes the bytes Serialize(new CompressedMovementPacket(...)) would, or null when they are not smaller.
    private byte[] WriteEnvelope(int uncompressedLength, byte[] payload, int payloadLength)
    {
        int length = EnvelopeLength(uncompressedLength, payloadLength, out int bodyLength);
        if (length >= uncompressedLength) return null;

        byte[] typeField = compressedTypeField;
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

    private static byte[] ReadTypeField(byte[] wrapped)
    {
        ProtoBufSerializer.ReadWrapper(wrapped, out int typeFieldLength, out _, out _);
        var typeField = new byte[typeFieldLength];
        Buffer.BlockCopy(wrapped, 0, typeField, 0, typeFieldLength);
        return typeField;
    }

    private static bool IsMovement(IPacket packet)
    {
        return packet.PacketType == PacketType.Movement ||
               packet.PacketType == PacketType.MountMovement;
    }
}
