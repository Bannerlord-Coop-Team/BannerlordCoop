using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Actions.Messages;

[ProtoContract(SkipConstructor = true)]
internal readonly struct ChangeGovernor : ICommand
{
    [ProtoMember(1)]
    public readonly string FortificationId;

    [ProtoMember(2)]
    public readonly string GovernorId;

    public ChangeGovernor(string fortificationId, string governorId)
    {
        FortificationId = fortificationId;
        GovernorId = governorId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RemoveGovernor : IEvent
{
    [ProtoMember(1)]
    public readonly string GovernorId;

    public RemoveGovernor(string governorId)
    {
        GovernorId = governorId;
    }
}
