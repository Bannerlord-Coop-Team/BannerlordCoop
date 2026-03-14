using Common.Messaging;
using GameInterface.AutoSyncPOC.Mapper;
using ProtoBuf;

namespace GameInterface.AutoSyncPOC.Messages;

[ProtoContract]
public readonly struct SetFieldNull : ICommand
{
    [ProtoMember(1)]
    public readonly AutoSyncFieldMap FieldMap;

    public SetFieldNull(AutoSyncFieldMap fieldMap)
    {
        FieldMap = fieldMap;
    }
}
