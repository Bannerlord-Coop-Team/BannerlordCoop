using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Quests.ArmyOfPoachers;

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestPoachersParty : ICommand
{
    [ProtoMember(1)] public readonly string IssueOwnerId;
    [ProtoMember(2)] public readonly string IssueId;

    public RequestPoachersParty(string issueOwnerId, string issueId)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkPoachersPartyCreated : ICommand
{
    [ProtoMember(1)] public readonly string IssueOwnerId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly string ControllerId;
    [ProtoMember(4)] public readonly string PartyId;

    public NetworkPoachersPartyCreated(string issueOwnerId, string issueId, string controllerId, string partyId)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        ControllerId = controllerId;
        PartyId = partyId;
    }
}
