using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Quests.LadysKnightOut;

public readonly struct LadysKnightOutLordTriggered : IEvent
{
    public IssueBase Issue { get; }
    public bool Complete { get; }
    public LadysKnightOutLordTriggered(IssueBase issue, bool complete) { Issue = issue; Complete = complete; }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct RequestLadysKnightOutLord : ICommand
{
    [ProtoMember(1)] public readonly string GiverId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly bool Complete;
    [ProtoMember(4)] public readonly float Difficulty;

    public RequestLadysKnightOutLord(string giverId, string issueId, bool complete, float difficulty)
    {
        GiverId = giverId;
        IssueId = issueId;
        Complete = complete;
        Difficulty = difficulty;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkLadysKnightOutLordAccepted : ICommand
{
    [ProtoMember(1)] public readonly string GiverId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly string ControllerId;
    [ProtoMember(4)] public readonly float Difficulty;
    [ProtoMember(5)] public readonly string StartLog;

    public NetworkLadysKnightOutLordAccepted(string giverId, string issueId, string controllerId, float difficulty, string startLog)
    {
        GiverId = giverId;
        IssueId = issueId;
        ControllerId = controllerId;
        Difficulty = difficulty;
        StartLog = startLog;
    }
}

public readonly struct LadysKnightOutLogAdded : IEvent
{
    public QuestBase Quest { get; }
    public JournalLog Log { get; }
    public LadysKnightOutLogAdded(QuestBase quest, JournalLog log) { Quest = quest; Log = log; }
}

public readonly struct LadysKnightOutIssueLogAdded : IEvent
{
    public IssueBase Issue { get; }
    public LadysKnightOutIssueLogAdded(IssueBase issue) { Issue = issue; }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkLadysKnightOutLog : ICommand
{
    [ProtoMember(1)] public readonly string GiverId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly string ControllerId;
    [ProtoMember(4)] public readonly string Text;
    [ProtoMember(5)] public readonly bool IsIssueLog;

    public NetworkLadysKnightOutLog(string giverId, string issueId, string controllerId, string text, bool isIssueLog)
    {
        GiverId = giverId;
        IssueId = issueId;
        ControllerId = controllerId;
        Text = text;
        IsIssueLog = isIssueLog;
    }
}
