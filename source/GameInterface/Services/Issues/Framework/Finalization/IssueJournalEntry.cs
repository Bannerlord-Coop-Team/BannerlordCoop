using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;

namespace GameInterface.Services.Issues.Framework.Finalization;

/// <summary>Preserves the native journal entry instead of recreating it at the receiver's current time.</summary>
[ProtoContract(SkipConstructor = true)]
public readonly struct IssueJournalEntry
{
    [ProtoMember(1)] public readonly CampaignTime Time;
    [ProtoMember(2)] public readonly TextObject Text;
    [ProtoMember(3)] public readonly TextObject TaskName;
    [ProtoMember(4)] public readonly int Progress;
    [ProtoMember(5)] public readonly int Range;
    [ProtoMember(6)] public readonly LogType Type;

    public IssueJournalEntry(JournalLog log)
    {
        Time = log.LogTime;
        Text = log.LogText;
        TaskName = log.TaskName;
        Progress = log.CurrentProgress;
        Range = log.Range;
        Type = log.Type;
    }

    public JournalLog ToJournalLog() => new JournalLog(Time, Text, TaskName, Progress, Range, Type);
}
