using Common;
using GameInterface.Services.Issues.Framework.Interface;
using System;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using TaleWorlds.Localization;
using Quest = TaleWorlds.CampaignSystem.Issues.RaidAnEnemyTerritoryIssueBehavior.RaidAnEnemyTerritoryQuest;

namespace GameInterface.Services.Issues.Quests.RaidAnEnemyTerritory;

internal sealed class RaidAnEnemyTerritoryQuestSolutionAcceptStrategy : IQuestSolutionAcceptStrategy
{
    public byte[] Capture(IssueBase issue)
    {
        if (!(issue.IssueQuest is Quest quest))
        {
            return Array.Empty<byte>();
        }

        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(quest.QuestDueTime.NumTicks);
            writer.Flush();
            return stream.ToArray();
        }
    }

    public void Apply(IssueBase issue, byte[] captured)
    {
        if (!(issue.IssueQuest is Quest quest))
        {
            return;
        }

        if (captured == null || captured.Length < sizeof(long))
        {
            throw new InvalidDataException("The raid quest acceptance is missing its deadline");
        }

        using (var stream = new MemoryStream(captured))
        using (var reader = new BinaryReader(stream))
        {
            quest.ChangeQuestDueTime(new CampaignTime(reader.ReadInt64()));
        }

        if (ModInformation.IsServer && quest._raidedVillagesTrackLog == null)
        {
            // Keep the acceptance stage in the saved mirror without starting it or raising UI events.
            quest._raidedVillagesTrackLog = new JournalLog(CampaignTime.Now, quest.QuestAcceptedByPlayerLog,
                new TextObject("{=RFH1lDMj}Raided Village Count"), quest._raidedVillages.Count, 3, LogType.Discreate);
            quest._journalEntries.Add(quest._raidedVillagesTrackLog);
        }
    }
}
