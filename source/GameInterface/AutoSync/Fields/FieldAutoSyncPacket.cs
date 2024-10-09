using Common.PacketHandlers;
using LiteNetLib;
using ProtoBuf;

namespace GameInterface.AutoSync.Fields;

[ProtoContract(SkipConstructor = true)]
public readonly struct FieldAutoSyncPacket : IPacket
{
    public FieldAutoSyncPacket(string instanceId, int typeId, int fieldId, byte[] value)
    {
        this.instanceId = instanceId;
        this.typeId = typeId;
        this.fieldId = fieldId;
        this.value = value;
    }

    [ProtoMember(1)]
    public readonly string instanceId;
    [ProtoMember(2)]
    public readonly int typeId;
    [ProtoMember(3)]
    public readonly int fieldId;
    [ProtoMember(4)]
    public readonly byte[] value;

    public readonly PacketType PacketType => PacketType.FieldAutoSync;

    public readonly DeliveryMethod DeliveryMethod => DeliveryMethod.ReliableOrdered;
}
