using Common.PacketHandlers;
using LiteNetLib;
using ProtoBuf;

namespace Missions.Agents.Packets;

[ProtoContract(SkipConstructor = true)]
public readonly struct CompressedMovementPacket : IPacket
{
    public DeliveryMethod DeliveryMethod => DeliveryMethod.Unreliable;

    public PacketType PacketType => PacketType.CompressedMovement;

    [ProtoMember(1)]
    public int UncompressedLength { get; }
    [ProtoMember(2)]
    public byte[] Payload { get; }

    public CompressedMovementPacket(int uncompressedLength, byte[] payload)
    {
        UncompressedLength = uncompressedLength;
        Payload = payload;
    }
}
