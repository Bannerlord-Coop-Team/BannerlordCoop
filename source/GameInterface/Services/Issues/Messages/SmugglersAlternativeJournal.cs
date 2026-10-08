using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Messages;

internal readonly struct SmugglersAlternativeJournalChanged : IEvent
{
    public readonly IssueBase Issue;
    public readonly IssueBase.IssueUpdateDetails Status;

    public SmugglersAlternativeJournalChanged(IssueBase issue, IssueBase.IssueUpdateDetails status)
    {
        Issue = issue;
        Status = status;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct SmugglersJournalEntry
{
    [ProtoMember(1)] public readonly long TimeTicks;
    [ProtoMember(2)] public readonly TextObject Text;
    [ProtoMember(3)] public readonly TextObject Task;
    [ProtoMember(4)] public readonly int Progress;
    [ProtoMember(5)] public readonly int Range;
    [ProtoMember(6)] public readonly LogType Type;

    public SmugglersJournalEntry(JournalLog log)
    {
        TimeTicks = log.LogTime.NumTicks;
        Text = log.LogText;
        Task = log.TaskName;
        Progress = log.CurrentProgress;
        Range = log.Range;
        Type = log.Type;
    }

    public JournalLog ToJournalLog() => new(new CampaignTime(TimeTicks), Text, Task, Progress, Range, Type);
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkSmugglersAlternativeJournal : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string OwnerId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly SmugglersJournalEntry[] Entries;
    [ProtoMember(4)] public readonly bool EffectsResolved;
    [ProtoMember(5)] public readonly IssueBase.IssueUpdateDetails Status;

    public NetworkSmugglersAlternativeJournal(string ownerId, string issueId,
        SmugglersJournalEntry[] entries, bool effectsResolved, IssueBase.IssueUpdateDetails status)
    {
        OwnerId = ownerId;
        IssueId = issueId;
        Entries = entries;
        EffectsResolved = effectsResolved;
        Status = status;
    }
}
