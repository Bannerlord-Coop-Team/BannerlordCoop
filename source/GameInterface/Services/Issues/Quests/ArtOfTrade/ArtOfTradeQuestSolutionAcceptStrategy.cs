using GameInterface.Services.Issues.Framework.Interface;
using System;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Issues;
using Quest = TaleWorlds.CampaignSystem.Issues.LandLordTheArtOfTheTradeIssueBehavior.LandLordTheArtOfTheTradeIssueQuest;

namespace GameInterface.Services.Issues.Quests.ArtOfTrade;

internal class ArtOfTradeQuestSolutionAcceptStrategy : IQuestSolutionAcceptStrategy
{
    public byte[] Capture(IssueBase issue)
    {
        if (!(issue.IssueQuest is Quest quest)) return Array.Empty<byte>();

        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(quest._selectedItemObjectCount);
            writer.Write(quest._targetDenarsToAchieve);
            writer.Write(quest.QuestDueTime.NumTicks);
            writer.Flush();
            return stream.ToArray();
        }
    }

    public void Apply(IssueBase issue, byte[] captured)
    {
        if (!(issue.IssueQuest is Quest quest) || captured.Length == 0) return;

        using (var reader = new BinaryReader(new MemoryStream(captured)))
        {
            quest._selectedItemObjectCount = reader.ReadInt32();
            quest._targetDenarsToAchieve = reader.ReadInt32();
            quest.ChangeQuestDueTime(new CampaignTime(reader.ReadInt64()));
        }
    }
}
