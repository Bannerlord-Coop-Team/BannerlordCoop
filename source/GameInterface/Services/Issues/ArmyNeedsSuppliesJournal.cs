using System.Linq;
using Common.Messaging;
using GameInterface.Surrogates;
using ProtoBuf;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Core;

namespace GameInterface.Services.Issues;

using Quest = ArmyNeedsSuppliesIssueBehavior.ArmyNeedsSuppliesIssueQuest;

[ProtoContract]
internal sealed class ArmyNeedsSuppliesAcceptance
{
    [ProtoMember(1)] public string ControllerId { get; set; }
    [ProtoMember(2)] public int Grain { get; set; }
    [ProtoMember(3)] public int Livestock { get; set; }
    [ProtoMember(4)] public int Wine { get; set; }
    [ProtoMember(5)] public CampaignTime DueTime { get; set; }
    [ProtoMember(7)] public ArmyNeedsSuppliesJournal Journal { get; set; }
}

[ProtoContract]
internal sealed class NetworkArmyNeedsSuppliesJournal : IServerToClientCommand
{
    [ProtoMember(1)] public string OwnerId { get; set; }
    [ProtoMember(2)] public int Generation { get; set; }
    [ProtoMember(3)] public string QuestId { get; set; }
    [ProtoMember(4)] public ArmyNeedsSuppliesJournal Journal { get; set; }
}

[ProtoContract]
internal sealed class ArmyNeedsSuppliesJournal
{
    [ProtoMember(1)] public Entry[] Entries { get; set; }
    [ProtoMember(2)] public int Grain { get; set; }
    [ProtoMember(3)] public int Livestock { get; set; }
    [ProtoMember(4)] public int Wine { get; set; }

    public ArmyNeedsSuppliesJournal() { }

    public ArmyNeedsSuppliesJournal(Quest quest)
    {
        Entries = quest.JournalEntries.Select(log => new Entry
        {
            Time = log.LogTime,
            Text = log.LogText,
            Task = log.TaskName,
            Progress = log.CurrentProgress,
            Range = log.Range,
            Type = log.Type,
        }).ToArray();
        Grain = quest._currentGrainAmount;
        Livestock = quest._currentLiveStockAmount;
        Wine = quest._currentWineAmount;
    }

    public void Apply(Quest quest)
    {
        if (Entries == null || Entries.Length < 4) return;
        quest._journalEntries.Clear();
        foreach (var entry in Entries)
        {
            quest._journalEntries.Add(new JournalLog(entry.Time, entry.Text, entry.Task.Text == null ? null : entry.Task,
                entry.Progress, entry.Range, entry.Type));
        }
        quest._grainLog = quest._journalEntries[1];
        quest._liveStockLog = quest._journalEntries[2];
        quest._wineLog = quest._journalEntries[3];
        quest._currentGrainAmount = Grain;
        quest._currentLiveStockAmount = Livestock;
        quest._currentWineAmount = Wine;
    }

    [ProtoContract]
    internal sealed class Entry
    {
        [ProtoMember(1)] public CampaignTime Time { get; set; }
        [ProtoMember(2)] public TextObjectSurrogate Text { get; set; }
        [ProtoMember(3)] public TextObjectSurrogate Task { get; set; }
        [ProtoMember(4)] public int Progress { get; set; }
        [ProtoMember(5)] public int Range { get; set; }
        [ProtoMember(6)] public LogType Type { get; set; }
    }
}
