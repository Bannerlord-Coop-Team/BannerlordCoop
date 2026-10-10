using Common.Messaging;
using ProtoBuf;

namespace Coop.Core.Server.Services.ItemRosters.Messages;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkRegisterItemHandle : IMessage
{
    [ProtoMember(1)]
    public readonly string StringId;

    [ProtoMember(2)]
    public readonly uint Handle;

    public NetworkRegisterItemHandle(string stringId, uint handle)
    {
        StringId = stringId;
        Handle = handle;
    }
}
