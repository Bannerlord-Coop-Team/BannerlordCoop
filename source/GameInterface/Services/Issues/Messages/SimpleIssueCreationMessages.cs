using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Messages;

public readonly struct SimpleIssueCreated : IEvent
{
    public readonly IssueBase Issue;

    public SimpleIssueCreated(IssueBase issue)
    {
        Issue = issue;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkSimpleIssueCreated : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string IssueKey;
    [ProtoMember(3)]
    public readonly int Generation;

    public NetworkSimpleIssueCreated(string ownerId, string issueKey, int generation)
    {
        OwnerId = ownerId;
        IssueKey = issueKey;
        Generation = generation;
    }
}
