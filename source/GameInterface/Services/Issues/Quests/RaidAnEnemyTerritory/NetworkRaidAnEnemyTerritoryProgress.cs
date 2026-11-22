using Common.Messaging;
using GameInterface.Services.Issues.Framework.Finalization;
using ProtoBuf;

namespace GameInterface.Services.Issues.Quests.RaidAnEnemyTerritory;

[ProtoContract(SkipConstructor = true)]
public readonly struct NetworkRaidAnEnemyTerritoryProgress : ICommand
{
    [ProtoMember(1)] public readonly string IssueOwnerId;
    [ProtoMember(2)] public readonly string IssueId;
    [ProtoMember(3)] public readonly string[] VillageIds;
    [ProtoMember(4)] public readonly IssueJournalEntry[] Entries;

    public NetworkRaidAnEnemyTerritoryProgress(
        string issueOwnerId,
        string issueId,
        string[] villageIds,
        IssueJournalEntry[] entries)
    {
        IssueOwnerId = issueOwnerId;
        IssueId = issueId;
        VillageIds = villageIds;
        Entries = entries;
    }
}
