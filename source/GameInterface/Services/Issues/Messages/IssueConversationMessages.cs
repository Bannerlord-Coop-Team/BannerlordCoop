using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Messages;

public readonly struct IssueConversationOpenedLocally : IEvent
{
    public readonly Hero IssueGiver;
    public readonly string ControllerId;

    public IssueConversationOpenedLocally(Hero issueGiver, string controllerId)
    {
        IssueGiver = issueGiver;
        ControllerId = controllerId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestIssueConversationOpened : ICommand
{
    [ProtoMember(1)]
    public readonly string IssueGiverId;

    public RequestIssueConversationOpened(string issueGiverId)
    {
        IssueGiverId = issueGiverId;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkIssueConversationAllowed : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string IssueGiverId;
    [ProtoMember(2)]
    public readonly int Generation;

    public NetworkIssueConversationAllowed(string issueGiverId, int generation)
    {
        IssueGiverId = issueGiverId;
        Generation = generation;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkIssueConversationDenied : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string IssueGiverId;

    public NetworkIssueConversationDenied(string issueGiverId)
    {
        IssueGiverId = issueGiverId;
    }
}
