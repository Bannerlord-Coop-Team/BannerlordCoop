using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Education.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkSendEducationNotification : ICommand
{
    [ProtoMember(1)]
    public readonly uint ChildToEducateHandle;

    [ProtoMember(2)]
    public readonly int Age;

    [ProtoMember(3)]
    public readonly short Education;

    public NetworkSendEducationNotification(
        uint childToEducateHandle,
        int age,
        short education)
    {
        ChildToEducateHandle = childToEducateHandle;
        Age = age;
        Education = education;
    }
}
