using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Education.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkEducationStageRejected : ICommand
{
    [ProtoMember(1)]
    public readonly uint ChildHandle;

    public NetworkEducationStageRejected(uint childHandle)
    {
        ChildHandle = childHandle;
    }
}
