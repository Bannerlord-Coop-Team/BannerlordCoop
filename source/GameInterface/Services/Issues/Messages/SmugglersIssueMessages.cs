using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Messages;

public readonly struct SmugglersQuestLogAdded : IEvent
{
    public readonly SmugglersIssueBehavior.SmugglersIssueQuest Quest;
    public readonly JournalLog Log;

    public SmugglersQuestLogAdded(SmugglersIssueBehavior.SmugglersIssueQuest quest, JournalLog log)
    {
        Quest = quest;
        Log = log;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkSmugglersQuestLog : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string QuestId;
    [ProtoMember(3)]
    public readonly int EntryIndex;
    [ProtoMember(4)]
    public readonly long TimeTicks;
    [ProtoMember(5)]
    public readonly TextObject Text;

    public NetworkSmugglersQuestLog(string ownerId, string questId, int entryIndex, long timeTicks, TextObject text)
    {
        OwnerId = ownerId;
        QuestId = questId;
        EntryIndex = entryIndex;
        TimeTicks = timeTicks;
        Text = text;
    }
}

public readonly struct SmugglersIssueCreated : IEvent
{
    public readonly SmugglersIssueBehavior.SmugglersIssue Issue;

    public SmugglersIssueCreated(SmugglersIssueBehavior.SmugglersIssue issue)
    {
        Issue = issue;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkSmugglersIssueCreated : IServerToClientCommand
{
    [ProtoMember(1)]
    public readonly string OwnerId;
    [ProtoMember(2)]
    public readonly string TargetSettlementId;
    [ProtoMember(3)]
    public readonly string OriginSettlementId;
    [ProtoMember(4)]
    public readonly int Generation;
    [ProtoMember(5)]
    public readonly string IssueId;
    [ProtoMember(6)]
    public readonly long CreationTimeTicks;
    [ProtoMember(7)]
    public readonly long DueTimeTicks;

    public NetworkSmugglersIssueCreated(string ownerId, string targetSettlementId,
        string originSettlementId, int generation, string issueId, long creationTimeTicks, long dueTimeTicks)
    {
        OwnerId = ownerId;
        TargetSettlementId = targetSettlementId;
        OriginSettlementId = originSettlementId;
        Generation = generation;
        IssueId = issueId;
        CreationTimeTicks = creationTimeTicks;
        DueTimeTicks = dueTimeTicks;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkQuestPlayerRemoved : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string ControllerId;
    [ProtoMember(2)] public readonly string HeroId;

    public NetworkQuestPlayerRemoved(string controllerId, string heroId)
    {
        ControllerId = controllerId;
        HeroId = heroId;
    }
}
