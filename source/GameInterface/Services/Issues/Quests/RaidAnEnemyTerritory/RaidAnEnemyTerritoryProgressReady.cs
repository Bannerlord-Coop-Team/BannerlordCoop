using Common.Messaging;

namespace GameInterface.Services.Issues.Quests.RaidAnEnemyTerritory;

internal readonly struct RaidAnEnemyTerritoryProgressReady : IEvent
{
    public readonly string IssueOwnerId;
    public readonly string IssueId;
    public readonly string[] VillageIds;
    public readonly RaidAnEnemyTerritoryJournalEntry[] Entries;

    public RaidAnEnemyTerritoryProgressReady(
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
