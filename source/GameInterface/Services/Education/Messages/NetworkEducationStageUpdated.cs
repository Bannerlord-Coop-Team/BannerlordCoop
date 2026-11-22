using Common.Messaging;
using ProtoBuf;
using System.Collections.Generic;

namespace GameInterface.Services.Education.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkEducationStageUpdated : ICommand
{
    [ProtoMember(1)]
    public readonly uint ChildHandle;

    [ProtoMember(2)]
    public readonly short Target;

    [ProtoMember(3)]
    public readonly List<string> ChosenOptions;

    public NetworkEducationStageUpdated(
        uint childHandle,
        short target,
        List<string> chosenOptions)
    {
        ChildHandle = childHandle;
        Target = target;
        ChosenOptions = chosenOptions;
    }
}
