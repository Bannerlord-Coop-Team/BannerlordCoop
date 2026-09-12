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

public readonly struct QuestSuccessTriggered : IEvent
{
    public readonly Hero Owner;
    public readonly string ControllerId;

    public QuestSuccessTriggered(Hero owner, string controllerId)
    {
        Owner = owner;
        ControllerId = controllerId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestIssueRemoved : ICommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly IssueFinalizeReason Reason;
    [ProtoMember(3)]
    public readonly int Generation;
    [ProtoMember(4)]
    public readonly byte Proof;

    public RequestIssueRemoved(string ownerId, IssueFinalizeReason reason, int generation, byte proof = 0)
    {
        OwnerId = ownerId;
        Reason = reason;
        Generation = generation;
        Proof = proof;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkIssueRemoved : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly IssueFinalizeReason Reason;
    [ProtoMember(3)]
    public readonly byte Proof;

    public NetworkIssueRemoved(string ownerId, IssueFinalizeReason reason, byte proof = 0)
    {
        OwnerId = ownerId;
        Reason = reason;
        Proof = proof;
    }
}
