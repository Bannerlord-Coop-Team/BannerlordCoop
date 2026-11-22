using Common.Messaging;
using GameInterface.Services.Issues.Framework.Finalization;

namespace GameInterface.Services.Issues.Quests.RaidAnEnemyTerritory;

public readonly struct RaidAnEnemyTerritoryProgressReady : IEvent
{
    public readonly string IssueOwnerId;
    public readonly string IssueId;
    public readonly string[] VillageIds;
    public readonly IssueJournalEntry[] Entries;

    public RaidAnEnemyTerritoryProgressReady(
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
