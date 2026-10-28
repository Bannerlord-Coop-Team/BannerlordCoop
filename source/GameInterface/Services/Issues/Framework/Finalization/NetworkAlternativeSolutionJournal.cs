using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Framework.Finalization;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkAlternativeSolutionJournal : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string IssueOwnerId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly IssueBase.IssueUpdateDetails Ending;
    [ProtoMember(4)] public readonly AlternativeSolutionJournalEntry[] Entries;

    public NetworkAlternativeSolutionJournal(string issueOwnerId, string issueId,
        IssueBase.IssueUpdateDetails ending, AlternativeSolutionJournalEntry[] entries)
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

    public AlternativeSolutionJournalEntry(JournalLog log)
    {
        LogTimeTicks = log.LogTime.NumTicks;
        // Resolve companion variables while the server still runs as the owner.
        Text = log.LogText?.ToString();
        TaskName = log.TaskName?.ToString();
        Progress = log.CurrentProgress;
        Range = log.Range;
        Type = log.Type;
    }

    public JournalLog ToJournalLog() => new JournalLog(new CampaignTime(LogTimeTicks),
        Text == null ? null : new TextObject(Text), TaskName == null ? null : new TextObject(TaskName), Progress, Range, Type);
}
