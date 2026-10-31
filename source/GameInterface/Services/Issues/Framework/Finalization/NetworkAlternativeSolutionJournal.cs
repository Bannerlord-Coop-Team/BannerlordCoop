using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Framework.Finalization;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkAlternativeSolutionJournal : ICommand
{
    [ProtoMember(1)] public readonly string IssueOwnerId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly IssueBase.IssueUpdateDetails Ending;
    [ProtoMember(4)] public readonly IssueJournalEntry[] Entries;

    public NetworkAlternativeSolutionJournal(string issueOwnerId, string issueId,
        IssueBase.IssueUpdateDetails ending, IssueJournalEntry[] entries)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        Ending = ending;
        Entries = entries;
    }
}

[ProtoContract(SkipConstructor = true)]
public readonly struct AlternativeSolutionJournalEntry
{
    [ProtoMember(1)] public readonly long LogTimeTicks;
    [ProtoMember(2)] public readonly string Text;
    [ProtoMember(3)] public readonly string TaskName;
    [ProtoMember(4)] public readonly int Progress;
    [ProtoMember(5)] public readonly int Range;
    [ProtoMember(6)] public readonly LogType Type;
    [ProtoMember(7)] public readonly TextObject NativeText;
    [ProtoMember(8)] public readonly TextObject NativeTaskName;

    public AlternativeSolutionJournalEntry(JournalLog log)
    {
        LogTimeTicks = log.LogTime.NumTicks;
        // Keep rendered fields for peers using the previous journal payload.
        Text = log.LogText?.ToString();
        TaskName = log.TaskName?.ToString();
        Progress = log.CurrentProgress;
        Range = log.Range;
        Type = log.Type;
        NativeText = log.LogText;
        NativeTaskName = log.TaskName;
    }

    public JournalLog ToJournalLog() => new JournalLog(new CampaignTime(LogTimeTicks),
        NativeText ?? (Text == null ? null : new TextObject(Text)),
        NativeTaskName ?? (TaskName == null ? null : new TextObject(TaskName)), Progress, Range, Type);
}
