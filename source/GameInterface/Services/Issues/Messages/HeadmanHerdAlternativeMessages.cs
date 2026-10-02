using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Messages;

internal readonly struct HeadmanHerdAlternativeUpdated : IEvent
{
    public readonly HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue Issue;
    public readonly IssueBase.IssueUpdateDetails Detail;

    public HeadmanHerdAlternativeUpdated(HeadmanNeedsToDeliverAHerdIssueBehavior.HeadmanNeedsToDeliverAHerdIssue issue,
        IssueBase.IssueUpdateDetails detail = IssueBase.IssueUpdateDetails.None)
    {
        Issue = issue;
        Detail = detail;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct HeadmanHerdJournalEntryData
{
    [ProtoMember(1)] public readonly CampaignTime Time;
    [ProtoMember(2)] public readonly TextObject Text;
    [ProtoMember(3)] public readonly TextObject TaskName;
    [ProtoMember(4)] public readonly int Progress;
    [ProtoMember(5)] public readonly int Range;
    [ProtoMember(6)] public readonly LogType Type;

    public HeadmanHerdJournalEntryData(JournalLog log)
    {
        Time = log.LogTime;
        Text = log.LogText;
        TaskName = log.TaskName;
        Progress = log.CurrentProgress;
        Range = log.Range;
        Type = log.Type;
    }

    public JournalLog ToLog() => new JournalLog(Time, Text, TaskName, Progress, Range, Type);
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkHeadmanHerdAlternativeUpdated : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string OwnerId;
    [ProtoMember(2)] public readonly int Generation;
    [ProtoMember(3)] public readonly string IssueId;
    [ProtoMember(4)] public readonly HeadmanHerdJournalEntryData[] Entries;
    [ProtoMember(5)] public readonly bool EffectsResolved;
    [ProtoMember(6)] public readonly IssueBase.IssueUpdateDetails Detail;

    public NetworkHeadmanHerdAlternativeUpdated(string ownerId, int generation, string issueId,
        HeadmanHerdJournalEntryData[] entries, bool effectsResolved, IssueBase.IssueUpdateDetails detail)
    {
        OwnerId = ownerId;
        Generation = generation;
        IssueId = issueId;
        Entries = entries;
        EffectsResolved = effectsResolved;
        Detail = detail;
    }
}
