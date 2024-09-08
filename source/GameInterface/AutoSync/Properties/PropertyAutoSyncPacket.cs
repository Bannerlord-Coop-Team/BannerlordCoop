using Common.PacketHandlers;
using LiteNetLib;
using ProtoBuf;

namespace GameInterface.AutoSync.Properties;

[ProtoContract(SkipConstructor = true)]
public readonly struct PropertyAutoSyncPacket : IPacket
{
    public PropertyAutoSyncPacket(string instanceId, int typeId, int propertyId, byte[] value)
    {
        this.instanceId = instanceId;
        this.typeId = typeId;
        this.propertyId = propertyId;
        this.value = value;
    }

    [ProtoMember(1)]
    public readonly string instanceId;
    [ProtoMember(2)]
    public readonly int typeId;
    [ProtoMember(3)]
    public readonly int propertyId;
    [ProtoMember(4)]
    public readonly byte[] value;

    public readonly PacketType PacketType => PacketType.PropertyAutoSync;

    public readonly DeliveryMethod DeliveryMethod => DeliveryMethod.ReliableOrdered;
}
