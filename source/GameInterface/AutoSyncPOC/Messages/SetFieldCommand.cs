using Common.Messaging;
using GameInterface.AutoSyncPOC.Mapper;
using ProtoBuf;

namespace GameInterface.AutoSyncPOC.Messages;

[ProtoContract]
public readonly struct SetFieldCommand : ICommand
{
    [ProtoMember(1)]
    public readonly AutoSyncFieldMap FieldMap;
    [ProtoMember(2)]
    public readonly byte[] SerializedValue;

    public SetFieldCommand(AutoSyncFieldMap fieldMap, byte[] serializedValue)
    {
        FieldMap = fieldMap;
        SerializedValue = serializedValue;
    }
}
