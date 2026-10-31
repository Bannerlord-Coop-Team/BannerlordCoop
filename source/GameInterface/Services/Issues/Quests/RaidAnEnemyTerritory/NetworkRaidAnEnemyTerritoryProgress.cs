using Common.Messaging;
using ProtoBuf;
using TaleWorlds.CampaignSystem;

namespace GameInterface.Services.Issues.Quests.RaidAnEnemyTerritory;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkRaidAnEnemyTerritoryProgress : IServerToClientCommand
{
    [ProtoMember(1)] public readonly string IssueOwnerId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly string[] VillageIds;
    [ProtoMember(4)] public readonly RaidAnEnemyTerritoryJournalEntry[] Entries;

    public NetworkRaidAnEnemyTerritoryProgress(
        string issueOwnerId,
        string issueId,
        string[] villageIds,
        RaidAnEnemyTerritoryJournalEntry[] entries)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        VillageIds = villageIds;
        Entries = entries;
    }
}

[ProtoContract(SkipConstructor = true)]
public sealed class RaidAnEnemyTerritoryJournalEntry
{
    [ProtoMember(1)] public CampaignTime Time { get; set; }
    [ProtoMember(2)] public TaleWorlds.Localization.TextObject Text { get; set; }
    [ProtoMember(3)] public TaleWorlds.Localization.TextObject Task { get; set; }
    [ProtoMember(4)] public int Progress { get; set; }
    [ProtoMember(5)] public int Range { get; set; }
    [ProtoMember(6)] public LogType Type { get; set; }
}
