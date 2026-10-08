using Common.Messaging;
using ProtoBuf;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Messages;

internal readonly struct BountyHuntersJournalChanged : IEvent
{
    public readonly IssueBase Issue;
    public readonly bool QuestJournal;
    public readonly IssueBase.IssueUpdateDetails Status;

    public BountyHuntersJournalChanged(IssueBase issue, bool questJournal, IssueBase.IssueUpdateDetails status = IssueBase.IssueUpdateDetails.None)
    {
        Issue = issue;
        QuestJournal = questJournal;
        Status = status;
    }
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct BountyHuntersJournalEntry
{
    [ProtoMember(1)] public readonly CampaignTime Time;
    [ProtoMember(2)] public readonly TextObject Text;
    [ProtoMember(3)] public readonly TextObject Task;
    [ProtoMember(4)] public readonly int Progress;
    [ProtoMember(5)] public readonly int Range;
    [ProtoMember(6)] public readonly LogType Type;

    public BountyHuntersJournalEntry(JournalLog log)
    {
        Time = log.LogTime;
        Text = log.LogText;
        Task = log.TaskName;
        Progress = log.CurrentProgress;
        Range = log.Range;
        Type = log.Type;
    }

    public JournalLog ToJournalLog() => new(Time, Text, Task, Progress, Range, Type);
}

[ProtoContract(SkipConstructor = true)]
internal readonly struct NetworkBountyHuntersJournal : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string GiverId;
    [ProtoMember(2)] public readonly int Generation;
    [ProtoMember(3)] public readonly bool QuestJournal;
    [ProtoMember(4)] public readonly BountyHuntersJournalEntry[] Entries;
    [ProtoMember(5)] public readonly IssueBase.IssueUpdateDetails Status;

    public NetworkBountyHuntersJournal(string giverId, int generation, bool questJournal,
        MBReadOnlyList<JournalLog> entries, IssueBase.IssueUpdateDetails status)
    {
        GiverId = giverId;
        Generation = generation;
        QuestJournal = questJournal;
        Entries = entries.Select(log => new BountyHuntersJournalEntry(log)).ToArray();
        Status = status;
    }
}
