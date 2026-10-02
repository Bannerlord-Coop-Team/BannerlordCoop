using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Messages;

public readonly struct ConquestIssueCreated : IEvent
{
    public readonly TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue Issue;

    public ConquestIssueCreated(TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssue issue)
    {
        Issue = issue;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkConquestIssueCreated : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string GiverId;
    [ProtoMember(2)] public readonly string TargetId;
    [ProtoMember(3)] public readonly int Generation;
    [ProtoMember(4)] public readonly CampaignTime DueTime;
    [ProtoMember(5)] public readonly string IssueId;

    public NetworkConquestIssueCreated(string giverId, string targetId, int generation, CampaignTime dueTime, string issueId)
    {
        GiverId = giverId;
        TargetId = targetId;
        Generation = generation;
        DueTime = dueTime;
        IssueId = issueId;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct ConquestAcceptFields
{
    [ProtoMember(1)] public readonly string ControllerId;
    [ProtoMember(2)] public readonly CampaignTime DueTime;
    [ProtoMember(3)] public readonly CampaignTime StartTime;
    [ProtoMember(4)] public readonly string QuestId;
    [ProtoMember(5)] public readonly int Generation;

    public ConquestAcceptFields(string controllerId, CampaignTime dueTime, CampaignTime startTime, string questId, int generation)
    {
        ControllerId = controllerId;
        DueTime = dueTime;
        StartTime = startTime;
        QuestId = questId;
        Generation = generation;
    }
}

public readonly struct ConquestQuestFinalizing : IEvent
{
    public readonly TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest Quest;

    public ConquestQuestFinalizing(TheConquestOfSettlementIssueBehavior.TheConquestOfSettlementIssueQuest quest)
    {
        Quest = quest;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkConquestQuestJournal : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string GiverId;
    [ProtoMember(2)] public readonly string QuestId;
    [ProtoMember(3)] public readonly int Generation;
    [ProtoMember(4)] public readonly TextObject[] Texts;
    [ProtoMember(5)] public readonly CampaignTime[] Times;

    public NetworkConquestQuestJournal(string giverId, string questId, int generation, TextObject[] texts, CampaignTime[] times)
    {
        GiverId = giverId;
        QuestId = questId;
        Generation = generation;
        Texts = texts;
        Times = times;
    }
}
