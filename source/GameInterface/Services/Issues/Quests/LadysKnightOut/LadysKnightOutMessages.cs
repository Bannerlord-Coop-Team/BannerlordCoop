using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;

namespace GameInterface.Services.Issues.Quests.LadysKnightOut;

internal sealed class LadysKnightOutLordTriggered : IEvent
{
    public IssueBase Issue { get; }
    public bool Complete { get; }
    public LadysKnightOutLordTriggered(IssueBase issue, bool complete) { Issue = issue; Complete = complete; }
}

[ProtoContract(SkipConstructor = true)]
internal sealed class RequestLadysKnightOutLord : ICommand
{
    [ProtoMember(1)] public string GiverId;
    [ProtoMember(2)] public string IssueId;
    [ProtoMember(3)] public bool Complete;
}

[ProtoContract(SkipConstructor = true)]
internal sealed class NetworkLadysKnightOutLordAccepted : ICommand
{
    [ProtoMember(1)] public string GiverId;
    [ProtoMember(2)] public string IssueId;
    [ProtoMember(3)] public string ControllerId;
    [ProtoMember(4)] public float Difficulty;
    [ProtoMember(5)] public string StartLog;
}

internal sealed class LadysKnightOutLogAdded : IEvent
{
    public QuestBase Quest { get; }
    public JournalLog Log { get; }
    public LadysKnightOutLogAdded(QuestBase quest, JournalLog log) { Quest = quest; Log = log; }
}

internal sealed class LadysKnightOutIssueLogAdded : IEvent
{
    public IssueBase Issue { get; }
    public LadysKnightOutIssueLogAdded(IssueBase issue) { Issue = issue; }
}

[ProtoContract(SkipConstructor = true)]
internal sealed class NetworkLadysKnightOutLog : ICommand
{
    [ProtoMember(1)] public string GiverId;
    [ProtoMember(2)] public string IssueId;
    [ProtoMember(3)] public string ControllerId;
    [ProtoMember(4)] public string Text;
    [ProtoMember(5)] public bool IsIssueLog;
}
