using Common.PacketHandlers;
using Common.Serialization;
using K4os.Compression.LZ4;
using Missions.Agents.Packets;
using System;
using System.Buffers;

namespace Missions.Services.Network;

public interface IMovementPacketCompressor
{
    byte[] Serialize(IPacket packet);
    bool TryRestore(IPacket packet, out IPacket restored);
}

public class MovementPacketCompressor : IMovementPacketCompressor
{
    internal const int MaxUncompressedBytes = 4096;

    private readonly ICommonSerializer serializer;

    public MovementPacketCompressor(ICommonSerializer serializer)
    {
        this.serializer = serializer;
    }

    public byte[] Serialize(IPacket packet)
    {
        byte[] original = serializer.Serialize(packet);
        if (!IsMovement(packet))
            return original;

        if (original.Length == 0 ||
            original.Length > MaxUncompressedBytes)
        {
            return original;
        }

        int maximumLength = LZ4Codec.MaximumOutputSize(original.Length);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maximumLength);
        try
        {
            int compressedLength = LZ4Codec.Encode(
                original, 0, original.Length,
                buffer, 0, maximumLength,
                LZ4Level.L00_FAST);
            if (compressedLength <= 0 || compressedLength >= original.Length)
            {
                return original;
            }

            var payload = new byte[compressedLength];
            Buffer.BlockCopy(buffer, 0, payload, 0, compressedLength);
            byte[] envelope = serializer.Serialize(
                new CompressedMovementPacket(original.Length, payload));
            return envelope.Length < original.Length ? envelope : original;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
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

    private static bool IsMovement(IPacket packet)
    {
        return packet.PacketType == PacketType.Movement ||
               packet.PacketType == PacketType.MountMovement;
    }
}
