using Common.Messaging;
using ProtoBuf;

namespace GameInterface.Services.Issues.Quests.Smugglers;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkSmugglersPartyCreated : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string IssueOwnerId;

    [ProtoMember(2)]
    public readonly string IssueId;

    [ProtoMember(3)]
    public readonly string ControllerId;

    [ProtoMember(4)]
    public readonly string PartyId;

    public NetworkSmugglersPartyCreated(string issueOwnerId, string issueId, string controllerId, string partyId)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        ControllerId = controllerId;
        PartyId = partyId;
    }
}
