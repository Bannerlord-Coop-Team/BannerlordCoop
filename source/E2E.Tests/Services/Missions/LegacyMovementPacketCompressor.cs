using Common.PacketHandlers;
using Common.Serialization;
using K4os.Compression.LZ4;
using Missions.Agents.Packets;
using Missions.Services.Network;
using System.Buffers;

namespace E2E.Tests.Services.Missions;

/// <summary>
/// Frozen copy of MovementPacketCompressor before #4487, which serialized every candidate and its LZ4 envelope with
/// protobuf-net. Equivalence tests compare the current compressor's datagrams against it byte for byte.
/// </summary>
internal sealed class LegacyMovementPacketCompressor : IMovementPacketCompressor
{
    private readonly ICommonSerializer serializer;

    public LegacyMovementPacketCompressor(ICommonSerializer serializer)
    {
        this.serializer = serializer;
    }

    public byte[] SerializeSnapshots(IPacket packet, Guid[] snapshotIds) => Serialize(packet);

    public byte[] Serialize(IPacket packet)
    {
        byte[] original = serializer.Serialize(packet);
        if (packet.PacketType != PacketType.Movement && packet.PacketType != PacketType.MountMovement)
            return original;

        if (original.Length == 0 || original.Length > MovementPacketCompressor.MaxUncompressedBytes)
            return original;

        int maximumLength = LZ4Codec.MaximumOutputSize(original.Length);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(maximumLength);
        try
        {
            int compressedLength = LZ4Codec.Encode(
                original, 0, original.Length,
                buffer, 0, maximumLength,
                LZ4Level.L00_FAST);
            if (compressedLength <= 0 || compressedLength >= original.Length)
                return original;

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

    public bool TryRestore(IPacket packet, out IPacket restored) =>
        throw new NotSupportedException();
}
