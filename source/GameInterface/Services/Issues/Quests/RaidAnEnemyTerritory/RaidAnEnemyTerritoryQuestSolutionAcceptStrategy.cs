using Common;
using GameInterface.Services.Issues.Framework.Interface;
using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest;

namespace GameInterface.Services.Issues.Quests.RaidAnEnemyTerritory;

internal sealed class RaidAnEnemyTerritoryQuestSolutionAcceptStrategy : IQuestSolutionAcceptStrategy
{
    public byte[] Capture(IssueBase issue) => Array.Empty<byte>();

    public void Apply(IssueBase issue, byte[] captured)
    {
        if (ModInformation.IsServer && issue.IssueQuest is Quest quest && quest._raidedVillagesTrackLog == null)
        {
            // Keep the acceptance stage in the saved mirror without starting it or raising UI events.
            quest._raidedVillagesTrackLog = new JournalLog(CampaignTime.Now, quest.QuestAcceptedByPlayerLog,
                new TextObject("{=RFH1lDMj}Raided Village Count"), quest._raidedVillages.Count,
                RaidAnEnemyTerritoryIssueBehavior.TargetRaidVillageCount, LogType.Discreate);
            quest._journalEntries.Add(quest._raidedVillagesTrackLog);
        }
    }
}
