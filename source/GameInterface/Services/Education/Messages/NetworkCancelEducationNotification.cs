using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Education.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkCancelEducationNotification : ICommand
{
    [ProtoMember(1)]
    public readonly uint ChildToEducateHandle;

    [ProtoMember(2)]
    public readonly int Age;

    public NetworkCancelEducationNotification(
        uint childToEducateHandle,
        int age)
    {
        ChildToEducateHandle = childToEducateHandle;
        Age = age;
    }
}
