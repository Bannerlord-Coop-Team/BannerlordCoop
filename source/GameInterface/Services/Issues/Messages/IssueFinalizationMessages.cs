using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Messages;

public enum IssueFinalizeReason : byte
{
    IssueOnly = 0,
    QuestSuccess = 1,
    QuestCancel = 2,
    QuestFail = 3,
    QuestTimeout = 4,
    QuestBetrayal = 5,
    RejectedAccept = 6,
    AlternativeSolutionSuccess = 7,
}

public readonly struct IssueFinalizedTriggered : IEvent
{
    public readonly Hero Owner;
    public readonly IssueFinalizeReason Reason;

    public IssueFinalizedTriggered(Hero owner, IssueFinalizeReason reason)
    {
        Owner = owner;
        Reason = reason;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestIssueRemoved : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly IssueFinalizeReason Reason;

    public RequestIssueRemoved(string ownerId, IssueFinalizeReason reason)
    {
        OwnerId = ownerId;
        Reason = reason;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkIssueRemoved : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly IssueFinalizeReason Reason;

    public NetworkIssueRemoved(string ownerId, IssueFinalizeReason reason)
    {
        OwnerId = ownerId;
        Reason = reason;
    }
}
