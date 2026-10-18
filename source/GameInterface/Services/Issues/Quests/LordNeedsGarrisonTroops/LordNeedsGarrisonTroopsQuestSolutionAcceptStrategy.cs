using GameInterface.Services.Issues.Framework.Interface;
using System;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.LordNeedsGarrisonTroopsIssueQuestBehavior.LordNeedsGarrisonTroopsIssueQuest;

namespace GameInterface.Services.Issues.Quests.LordNeedsGarrisonTroops;

internal class LordNeedsGarrisonTroopsQuestSolutionAcceptStrategy : IQuestSolutionAcceptStrategy
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
            writer.Write(quest._requestedTroopAmount);
            writer.Write(quest._rewardGold);
            writer.Write(quest.QuestDueTime.NumTicks);
            writer.Flush();
            return stream.ToArray();
        }
    }

    public void Apply(IssueBase issue, byte[] captured)
    {
        if (!(issue.IssueQuest is Quest quest) || captured == null || captured.Length == 0)
        {
            return;
        }

        using (var stream = new MemoryStream(captured))
        using (var reader = new BinaryReader(stream))
        {
            quest._requestedTroopAmount = reader.ReadInt32();
            quest._rewardGold = reader.ReadInt32();
            quest.QuestDueTime = new CampaignTime(reader.ReadInt64());
        }
    }
}
