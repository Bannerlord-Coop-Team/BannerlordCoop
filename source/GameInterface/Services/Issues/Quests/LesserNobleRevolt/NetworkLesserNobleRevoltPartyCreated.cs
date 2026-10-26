using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Quests.LesserNobleRevolt;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkLesserNobleRevoltPartyCreated : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string IssueOwnerId;

    [ProtoMember(2)]
    public readonly string IssueId;

    [ProtoMember(3)]
    public readonly string ControllerId;

    [ProtoMember(4)]
    public readonly string PartyId;

    public NetworkLesserNobleRevoltPartyCreated(string issueOwnerId, string issueId, string controllerId, string partyId)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        ControllerId = controllerId;
        PartyId = partyId;
    }
}
