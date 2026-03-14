using ProtoBuf;

namespace GameInterface.AutoSyncPOC.Mapper;

[ProtoContract]
public readonly struct AutoSyncFieldMap
{
    [ProtoMember(1)]
    public readonly ulong NetworkId;

    [ProtoMember(2)]
    public readonly int FieldId;

    public AutoSyncFieldMap(ulong networkId, int fieldId)
    {
        NetworkId = networkId;
        FieldId = fieldId;
    }
}
